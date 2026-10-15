using System.Text.Json;
using CookerPods.Diagnostic.Core;
using Xunit;

namespace CookerPods.Diagnostic.Tests;

public sealed class LiveMonitorTests
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public void Advance(int seconds) => Now = Now.AddSeconds(seconds);
    }

    private static byte[] Frame(byte buds = 0xa9, byte power = 0x0f)
    {
        byte[] bytes = new byte[27];
        bytes[0] = 7; bytes[1] = 25; bytes[2] = 1; bytes[3] = 0x24; bytes[4] = 0x20;
        bytes[6] = buds; bytes[7] = power; bytes[10] = 0x80;
        return bytes;
    }

    private static string OnlyLabel(LiveMonitorSession session) => Assert.Single(session.Snapshot().Observations.Candidates).Label;

    [Fact]
    public void ReceivingAFrameDoesNotAutomaticallySelectOrClaimAnAirPodsIdentity()
    {
        using var session = new LiveMonitorSession(new Clock());
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame());
        var snapshot = session.Snapshot();
        Assert.Equal(MonitorReadingState.ChooseCandidate, snapshot.State);
        Assert.Null(snapshot.SelectedCandidateLabel);
        Assert.False(snapshot.IsFresh);
        Assert.Null(snapshot.DisplaySample);
    }

    [Fact]
    public void SelectionUsesTheCandidateNotAFriendlyNameAndUnknownCaseStaysUnknown()
    {
        using var session = new LiveMonitorSession(new Clock());
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame());
        Assert.True(session.SelectCandidate(OnlyLabel(session)));
        var snapshot = session.Snapshot();
        Assert.Equal(MonitorReadingState.Live, snapshot.State);
        Assert.True(snapshot.IsFresh);
        Assert.Equal(90, snapshot.DisplaySample!.Estimate.EarbudSlotAPercent);
        Assert.Equal(100, snapshot.DisplaySample.Estimate.EarbudSlotBPercent);
        Assert.Null(snapshot.DisplaySample.Estimate.CasePercent);
    }

    [Fact]
    public void ReadingExpiresEvenWhenNoNewPacketArrives()
    {
        var clock = new Clock();
        using var session = new LiveMonitorSession(clock);
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame());
        Assert.True(session.SelectCandidate(OnlyLabel(session)));
        clock.Advance(16);
        var snapshot = session.Snapshot();
        Assert.Equal(MonitorReadingState.Stale, snapshot.State);
        Assert.False(snapshot.IsFresh);
        Assert.Equal(16, snapshot.SampleAgeSeconds);
        Assert.Equal(90, snapshot.DisplaySample!.Estimate.EarbudSlotAPercent);
    }

    [Fact]
    public void UnknownLatestPacketDoesNotFreshenAnOlderEstimate()
    {
        var clock = new Clock();
        using var session = new LiveMonitorSession(clock);
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame());
        Assert.True(session.SelectCandidate(OnlyLabel(session)));
        clock.Advance(2);
        session.Observe(epoch, 123, [7, 1, 99]);
        Assert.False(session.Snapshot().IsFresh);
        Assert.Equal(2, session.Snapshot().SampleAgeSeconds);
    }

    [Fact]
    public void PauseAndResumeNeedANewPacketForTheSelectedCandidate()
    {
        using var session = new LiveMonitorSession(new Clock());
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame());
        Assert.True(session.SelectCandidate(OnlyLabel(session)));
        session.Pause();
        Assert.Equal(MonitorReadingState.Paused, session.Snapshot().State);
        var next = session.BeginScan();
        Assert.False(session.Snapshot().IsFresh);
        session.Observe(epoch, 123, Frame(0x21)); // queued callback from the stopped run
        Assert.False(session.Snapshot().IsFresh);
        session.Observe(next, 456, Frame(0x32)); // another nearby candidate cannot freshen the selected one
        Assert.False(session.Snapshot().IsFresh);
        session.Observe(next, 123, Frame());
        Assert.True(session.Snapshot().IsFresh);
    }

    [Fact]
    public void OldStoppedEventCannotStopANewerRun()
    {
        using var session = new LiveMonitorSession(new Clock());
        var old = session.BeginScan();
        session.Pause();
        session.BeginScan();
        session.ScanStopped(old, new("test", "OldFailure", "0"));
        Assert.True(session.Snapshot().Scanning);
        Assert.Null(session.Snapshot().ScanIssue);
    }

    [Fact]
    public void EvictionNeverReusesTheSelectionLabelOrSwitchesToASimilarBroadcast()
    {
        var clock = new Clock();
        using var session = new LiveMonitorSession(clock, capacity: 1);
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame());
        string label = OnlyLabel(session);
        Assert.True(session.SelectCandidate(label));
        clock.Advance(1);
        session.Observe(epoch, 456, Frame());
        var snapshot = session.Snapshot();
        Assert.Equal(MonitorReadingState.SelectionExpired, snapshot.State);
        Assert.Null(snapshot.SelectedCandidateLabel);
        Assert.Null(snapshot.DisplaySample);
        Assert.NotEqual(label, Assert.Single(snapshot.Observations.Candidates).Label);
        Assert.Equal(1, snapshot.Observations.EvictedCandidates);
    }

    [Fact]
    public void LongInactivityRemovesTheCandidateAndRequiresReselection()
    {
        var clock = new Clock();
        using var session = new LiveMonitorSession(clock);
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame());
        var old = OnlyLabel(session);
        Assert.True(session.SelectCandidate(old));
        clock.Advance(121);
        Assert.Equal(MonitorReadingState.SelectionExpired, session.Snapshot().State);
        Assert.Empty(session.Snapshot().Observations.Candidates);
        session.Observe(epoch, 123, Frame());
        Assert.NotEqual(old, OnlyLabel(session));
        Assert.Null(session.Snapshot().SelectedCandidateLabel);
    }

    [Fact]
    public void UnknownOrUnavailableOrExpiredCandidatesCannotBeSelected()
    {
        var clock = new Clock();
        using var session = new LiveMonitorSession(clock);
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame(0xff, 0xff));
        Assert.False(session.SelectCandidate(OnlyLabel(session)));
        session.Observe(epoch, 123, Frame());
        clock.Advance(16);
        Assert.False(session.SelectCandidate(OnlyLabel(session)));
        Assert.False(session.SelectCandidate("missing"));
    }

    [Fact]
    public void ReportsContainNoPrivateRadioKeysAndClearlyLabelTheSelection()
    {
        const ulong address = 0xAABBCCDDEEFF;
        using var session = new LiveMonitorSession(new Clock());
        var epoch = session.BeginScan();
        session.Observe(epoch, address, Frame());
        session.SelectCandidate(OnlyLabel(session));
        string json = session.ExportJson();
        Assert.DoesNotContain("AABBCCDDEEFF", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(address.ToString(), json);
        Assert.Contains("user-directed", json);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("live-monitor", document.RootElement.GetProperty("reportKind").GetString());
        Assert.Equal("Nearby candidate 1", document.RootElement.GetProperty("snapshot").GetProperty("selectedCandidateLabel").GetString());
    }

    [Fact]
    public void DisposeRejectsLateCallbacksAndSelection()
    {
        var session = new LiveMonitorSession(new Clock());
        var epoch = session.BeginScan();
        session.Observe(epoch, 123, Frame());
        session.Dispose();
        session.Observe(epoch, 456, Frame());
        Assert.False(session.SelectCandidate("Nearby candidate 1"));
        Assert.False(session.Snapshot().Scanning);
        Assert.Single(session.Snapshot().Observations.Candidates);
        Assert.Throws<ObjectDisposedException>(() => session.BeginScan());
    }
}
