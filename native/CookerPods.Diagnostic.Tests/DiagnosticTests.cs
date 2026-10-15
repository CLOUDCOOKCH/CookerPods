using System.Text.Json;
using CookerPods.Diagnostic.Core;
using Xunit;

namespace CookerPods.Diagnostic.Tests;

public sealed class DiagnosticTests
{
    // Independently authored synthetic format fixture; no device capture or copied implementation.
    private static byte[] Frame(byte buds = 0x87, byte power = 0x65)
    {
        byte[] result = new byte[27];
        result[0] = 7; result[1] = 25; result[2] = 1; result[3] = 0x14; result[4] = 0x20;
        result[6] = buds; result[7] = power;
        return result;
    }

    [Fact]
    public void DecodeReturnsCoarseSlotsWithoutLeftRightOrIdentityClaim()
    {
        var result = ProximityFrame.Decode(Frame());
        Assert.NotNull(result);
        Assert.Equal(70, result.EarbudSlotAPercent);
        Assert.Equal(80, result.EarbudSlotBPercent);
        Assert.Equal(50, result.CasePercent);
        Assert.Contains("unverified", result.Confidence);
    }

    [Fact]
    public void UnavailableIsNotZeroAndZeroIsValid()
    {
        var unknown = ProximityFrame.Decode(Frame(0xff, 0xff));
        Assert.NotNull(unknown);
        Assert.Null(unknown.CasePercent);
        Assert.Null(unknown.EarbudSlotAPercent);
        Assert.Null(unknown.EarbudSlotBPercent);
        Assert.Equal(0, ProximityFrame.Decode(Frame(0, 0))!.CasePercent);
    }

    [Theory]
    [InlineData(0xb7, 0x65)] [InlineData(0x8c, 0x65)] [InlineData(0x87, 0x6d)]
    public void ReservedNibbleDoesNotBecomeMoreThanOneHundredPercent(byte buds, byte power) =>
        Assert.Null(ProximityFrame.Decode(Frame(buds, power)));

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(10)] [InlineData(26)] [InlineData(28)] [InlineData(1024)]
    public void OtherSizesAreNotGuessed(int size) => Assert.Null(ProximityFrame.Decode(new byte[size]));

    [Theory]
    [InlineData(0, 0x10)] [InlineData(1, 24)] [InlineData(2, 2)] [InlineData(10, 1)]
    public void UnrecognizedTypeOrLayoutIsNotDecoded(int offset, byte value)
    {
        var bytes = Frame(); bytes[offset] = value;
        Assert.Null(ProximityFrame.Decode(bytes));
    }

    [Fact]
    public void UnknownLatestFrameClearsPreviousEstimate()
    {
        var collector = new ObservationCollector();
        collector.Observe(123, Frame(), DateTimeOffset.UtcNow);
        collector.Observe(123, [7, 2, 99, 0], DateTimeOffset.UtcNow);
        var entry = Assert.Single(collector.Snapshot().Candidates);
        Assert.Equal(2, entry.Observations);
        Assert.Null(entry.ExperimentalEstimate);
    }

    [Fact]
    public void LateOlderCallbackDoesNotReplaceNewerTelemetry()
    {
        var collector = new ObservationCollector();
        var now = DateTimeOffset.UtcNow;
        collector.Observe(123, Frame(0x87), now);
        collector.Observe(123, Frame(0x21), now.AddSeconds(-10));
        var entry = Assert.Single(collector.Snapshot().Candidates);
        Assert.Equal(2, entry.Observations);
        Assert.Equal(now, entry.LastSeenUtc);
        Assert.Equal(70, entry.ExperimentalEstimate!.EarbudSlotAPercent);
    }

    [Fact]
    public void LimitsAndCloseBoundMemoryAndStopLateCallbacks()
    {
        var collector = new ObservationCollector(2);
        for (ulong i = 0; i < 20; i++) collector.Observe(i, Frame(), DateTimeOffset.UtcNow);
        collector.Observe(0, new byte[513], DateTimeOffset.UtcNow);
        var frozen = collector.Snapshot(close: true);
        collector.Observe(0, Frame(), DateTimeOffset.UtcNow);
        Assert.Equal(2, frozen.Candidates.Count);
        Assert.Equal(18, frozen.DroppedNewCandidates);
        Assert.Equal(1, frozen.IgnoredOversizedSections);
        Assert.Equal(frozen.AppleManufacturerSections, collector.Snapshot().AppleManufacturerSections);
    }

    [Fact]
    public void OtherAppleMessageTypesAreNotAttributedToAnAirPodsCandidate()
    {
        var collector = new ObservationCollector();
        collector.Observe(123, [0x10, 2, 4, 8], DateTimeOffset.UtcNow);
        Assert.Empty(collector.Snapshot().Candidates);
        Assert.Equal(1, collector.Snapshot().AppleManufacturerSections);
    }

    [Fact]
    public void ReportsExcludeAddressesPayloadsAndExceptionText()
    {
        const ulong address = 0xAABBCCDDEEFF;
        var collector = new ObservationCollector();
        collector.Observe(address, Frame(), DateTimeOffset.UtcNow);
        var report = new DiagnosticReport { Observations = collector.Snapshot() };
        report.Issues.Add(DiagnosticIssue.From("test", new Exception("private name and AA:BB:CC:DD:EE:FF")));
        string json = report.ToJson();
        Assert.DoesNotContain("AABBCCDDEEFF", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(address.ToString(), json);
        Assert.DoesNotContain("private name", json);
        Assert.DoesNotContain("AA:BB", json);
        Assert.DoesNotContain("payload\"", json, StringComparison.OrdinalIgnoreCase);
        using var parsed = JsonDocument.Parse(json);
        Assert.Equal(2, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
    }

    [Fact]
    public void ConcurrentCallbacksDoNotLoseCounts()
    {
        var collector = new ObservationCollector();
        Parallel.For(0, 500, _ => collector.Observe(123, Frame(), DateTimeOffset.UtcNow));
        var snapshot = collector.Snapshot();
        Assert.Equal(500, snapshot.AppleManufacturerSections);
        Assert.Equal(500, Assert.Single(snapshot.Candidates).Observations);
    }

    [Theory]
    [InlineData(0, 16, ProximityDecodeOutcome.NotProximityMessage)]
    [InlineData(1, 24, ProximityDecodeOutcome.UnsupportedDeclaredLength)]
    [InlineData(2, 2, ProximityDecodeOutcome.UnsupportedLayoutVersion)]
    [InlineData(10, 1, ProximityDecodeOutcome.UnsupportedByte10)]
    [InlineData(6, 0xbb, ProximityDecodeOutcome.ReservedBatteryNibble)]
    public void RejectionExplainsFirstFailedCheckWithoutReturningRawFields(int offset, byte value, ProximityDecodeOutcome expected)
    {
        var bytes = Frame(); bytes[offset] = value;
        var result = ProximityFrame.Analyze(bytes);
        Assert.Equal(expected, result.Outcome);
        Assert.Null(result.Estimate);
    }

    [Fact]
    public void LaterUnknownFramePreservesHistoricalReadingWithItsOriginalTimestamp()
    {
        var collector = new ObservationCollector();
        var now = DateTimeOffset.UtcNow;
        collector.Observe(123, Frame(0xaa, 0x08), now);
        var unknown = Frame(); unknown[10] = 1;
        collector.Observe(123, unknown, now.AddSeconds(10));
        var item = Assert.Single(collector.Snapshot().Candidates);
        Assert.Null(item.ExperimentalEstimate);
        Assert.Equal(ProximityDecodeOutcome.UnsupportedByte10, item.LatestDecodeOutcome);
        Assert.Equal(now.AddSeconds(10), item.LastSeenUtc);
        Assert.Equal(now, item.LastDecodedEstimate!.ObservedAtUtc);
        Assert.Equal(100, item.LastDecodedEstimate.Estimate.EarbudSlotAPercent);
        Assert.Equal(80, item.LastDecodedEstimate.Estimate.CasePercent);
        Assert.Equal(1, item.DecodeOutcomes[ProximityDecodeOutcome.Decoded]);
        Assert.Equal(1, item.DecodeOutcomes[ProximityDecodeOutcome.UnsupportedByte10]);
    }

    [Fact]
    public void DecodeHistoryIsBoundedAndOrderedByEventTimeIncludingLateCallbacks()
    {
        var collector = new ObservationCollector();
        var now = DateTimeOffset.UtcNow;
        foreach (var i in Enumerable.Range(0, 100).Reverse()) collector.Observe(123, Frame(), now.AddSeconds(i));
        var item = Assert.Single(collector.Snapshot().Candidates);
        Assert.Equal(ObservationCollector.HistoryLimit, item.RecentDecodedEstimates.Count);
        Assert.Equal(now.AddSeconds(92), item.RecentDecodedEstimates[0].ObservedAtUtc);
        Assert.Equal(now.AddSeconds(99), item.LastDecodedEstimate!.ObservedAtUtc);
        Assert.Equal(100, item.DecodeOutcomes[ProximityDecodeOutcome.Decoded]);
        Assert.Equal(item.Observations, item.DecodeOutcomes.Values.Sum());
    }

    [Fact]
    public void SnapshotsDoNotChangeAsMoreFramesArrive()
    {
        var collector = new ObservationCollector();
        var now = DateTimeOffset.UtcNow;
        collector.Observe(123, Frame(), now);
        var first = Assert.Single(collector.Snapshot().Candidates);
        collector.Observe(123, Frame(), now.AddSeconds(1));
        Assert.Single(first.RecentDecodedEstimates);
        Assert.Equal(1, first.DecodeOutcomes[ProximityDecodeOutcome.Decoded]);
    }

    [Fact]
    public void ReportSerializesNamedOutcomesHistoryAndUnmatchedEntriesWithoutPrivateNames()
    {
        var collector = new ObservationCollector();
        collector.Observe(0xAABBCCDDEEFF, Frame(), DateTimeOffset.UtcNow);
        var invalid = Frame(); invalid[10] = 1;
        collector.Observe(0xAABBCCDDEEFF, invalid, DateTimeOffset.UtcNow);
        var report = new DiagnosticReport { Observations = collector.Snapshot() };
        report.PairedDevices.Add(new("ClassicAssociationEndpoint candidate 1", false, "Connected",
            "ClassicAssociationEndpoint", "System.Devices.Aep.IsConnected"));
        string json = report.ToJson();
        Assert.Contains("UnsupportedByte10", json);
        Assert.DoesNotContain("AABBCCDDEEFF", json);
        using var parsed = JsonDocument.Parse(json);
        var candidate = parsed.RootElement.GetProperty("observations").GetProperty("candidates")[0];
        Assert.Equal(1, candidate.GetProperty("decodeOutcomes").GetProperty("Decoded").GetInt32());
        Assert.NotEqual(JsonValueKind.Null, candidate.GetProperty("lastDecodedEstimate").ValueKind);
        Assert.Equal(JsonValueKind.Null, candidate.GetProperty("experimentalEstimate").ValueKind);
        Assert.False(parsed.RootElement.GetProperty("pairedDevices")[0].GetProperty("nameSuggestsAirPods").GetBoolean());
    }
}
