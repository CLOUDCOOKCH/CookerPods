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
        Assert.Equal(1, parsed.RootElement.GetProperty("schemaVersion").GetInt32());
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
}
