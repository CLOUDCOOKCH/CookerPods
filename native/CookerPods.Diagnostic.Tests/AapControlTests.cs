using CookerPods.Diagnostic.Core;
using System.Threading.Channels;
using Xunit;

namespace CookerPods.Diagnostic.Tests;

public sealed class AapControlTests
{
    private sealed class Clock : TimeProvider
    {
        public long Ticks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public override long GetTimestamp() => Ticks;
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch.AddTicks(Ticks);
        public void Advance(int seconds) => Ticks += TimeSpan.FromSeconds(seconds).Ticks;
    }
    private static byte[] Report(byte attribute, byte value) => [4,0,4,0,9,0,attribute,value,0,0,0];

    [Fact]
    public void NoiseReportsRequireFullEnvelopeAndKnownMode()
    {
        Assert.True(AapProtocol.TryDecode(Report(0x0d, 3), out var n));
        Assert.Equal(new NoiseModeReport(NoiseMode.Transparency), n);
        foreach (byte value in new byte[] {0,5,255}) Assert.False(AapProtocol.TryDecode(Report(0x0d,value),out _));
        var malformed=Report(0x0d,2); malformed[10]=1;
        Assert.False(AapProtocol.TryDecode(malformed,out _));
        Assert.False(AapProtocol.TryDecode(new byte[2049],out _));
        Assert.False(AapProtocol.TryDecode(Report(0x0d,2)[..7],out _));
    }
    [Fact]
    public void BatteryComponentIdsComeFromAapAndUnavailableIsNotZero()
    {
        byte[] packet=[4,0,4,0,4,0,3, 4,1,83,2,1, 2,1,0,2,1, 8,1,255,4,1];
        Assert.True(AapProtocol.TryDecode(packet,out var result));
        var battery=Assert.IsType<BatteryReport>(result).Components;
        Assert.Equal(AapComponent.Left,battery[0].Component);Assert.Equal(83,battery[0].Percent);
        Assert.Equal(AapComponent.Right,battery[1].Component);Assert.Equal(0,battery[1].Percent);
        Assert.Null(battery[2].Percent);
        packet[12]=4; Assert.False(AapProtocol.TryDecode(packet,out _)); // duplicate component
    }
    [Fact]
    public void BatteryMalformedCountsAndReservedPercentAreRejected()
    {
        Assert.False(AapProtocol.TryDecode([4,0,4,0,4,0,0],out _));
        Assert.False(AapProtocol.TryDecode([4,0,4,0,4,0,2,4,1,80,2,1],out _));
        Assert.False(AapProtocol.TryDecode([4,0,4,0,4,0,1,4,1,101,2,1],out _));
        Assert.False(AapProtocol.TryDecode([4,0,4,0,4,0,1,4,1,80,3,1],out _));
    }
    [Fact]
    public void EarRolesRemainPrimarySecondaryAndUnknownFeaturesStayUnknown()
    {
        Assert.True(AapProtocol.TryDecode([4,0,4,0,6,0,0,2],out var n));
        Assert.Equal(new EarPresenceReport(PodPresence.InEar,PodPresence.InCase),n);
        Assert.False(AapProtocol.TryDecode(Report(0x28,0),out _));
        Assert.False(AapProtocol.TryDecode(Report(0x99,1),out _));
        Assert.Throws<ArgumentOutOfRangeException>(()=>AapProtocol.SetNoiseMode((NoiseMode)0));
        Assert.Throws<ArgumentOutOfRangeException>(()=>AapProtocol.SetBoolean((AapSetting)0xff,true));
    }
    [Fact]
    public void ConnectingAndBatteryAloneNeverEnableUnknownNoiseSetting()
    {
        var s=new AapControlState();var epoch=s.BeginConnection();
        Assert.False(s.TryRequestMode(epoch,NoiseMode.Transparency,out _));
        s.ChannelOpened(epoch);s.Observe(epoch,[4,0,4,0,4,0,1,4,1,80,2,1]);
        Assert.False(s.Snapshot().CanChangeNoiseMode);
        Assert.False(s.Snapshot().CanChange(AapSetting.ConversationAwareness));
    }
    [Fact]
    public void SendingACommandDoesNotChangeReportedModeAndOnlyMatchingReportResolvesPending()
    {
        var s=new AapControlState();var epoch=s.BeginConnection();s.ChannelOpened(epoch);
        s.Observe(epoch,Report(0x0d,2));
        Assert.True(s.TryRequestMode(epoch,NoiseMode.Transparency,out var command));
        Assert.Equal(Report(0x0d,3),command);
        Assert.Equal(NoiseMode.NoiseCancellation,s.Snapshot().ReportedNoiseMode);
        Assert.Equal(CommandOutcome.Pending,s.Snapshot().LastCommandOutcome);
        Assert.False(s.TryRequestMode(epoch,NoiseMode.Off,out _));
        s.Observe(epoch,Report(0x0d,2)); Assert.NotNull(s.Snapshot().Pending);
        s.Observe(epoch,Report(0x28,1));Assert.NotNull(s.Snapshot().Pending);
        s.Observe(epoch,Report(0x0d,3));Assert.Null(s.Snapshot().Pending);
        Assert.Equal(CommandOutcome.DeviceReported,s.Snapshot().LastCommandOutcome);
        Assert.Equal(NoiseMode.Transparency,s.Snapshot().ReportedNoiseMode);
    }
    [Fact]
    public void AdaptiveRequiresReportedCapabilityAndResetsOnAnotherConnection()
    {
        var s=new AapControlState();var e=s.BeginConnection();s.ChannelOpened(e);s.Observe(e,Report(0x0d,2));
        Assert.False(s.TryRequestMode(e,NoiseMode.Adaptive,out _));
        s.Observe(e,Report(0x0d,4));s.Observe(e,Report(0x0d,2));
        Assert.True(s.TryRequestMode(e,NoiseMode.Adaptive,out _));
        e=s.BeginConnection();s.ChannelOpened(e);s.Observe(e,Report(0x0d,2));
        Assert.False(s.TryRequestMode(e,NoiseMode.Adaptive,out _));
    }
    [Fact]
    public void TimeoutNeverBecomesSuccessOrOptimisticSetting()
    {
        var clock=new Clock();var s=new AapControlState(clock);var e=s.BeginConnection();s.ChannelOpened(e);
        s.Observe(e,Report(0x28,2));Assert.True(s.TryRequestSetting(e,AapSetting.ConversationAwareness,true,out _));
        clock.Advance(8);
        Assert.Equal(CommandOutcome.Unconfirmed,s.Snapshot().LastCommandOutcome);
        Assert.False(s.Snapshot().ReportedConversationAwareness);
        s.Observe(e,Report(0x28,1));
        Assert.True(s.Snapshot().ReportedConversationAwareness);
        Assert.Equal(CommandOutcome.Unconfirmed,s.Snapshot().LastCommandOutcome);
    }
    [Fact]
    public void DisconnectAndReconnectInvalidateOldPacketsAndPendingWrites()
    {
        var s=new AapControlState();long old=s.BeginConnection();s.ChannelOpened(old);s.Observe(old,Report(0x0d,2));
        s.TryRequestMode(old,NoiseMode.Transparency,out _);s.Disconnect(old);
        Assert.Equal(CommandOutcome.Interrupted,s.Snapshot().LastCommandOutcome);
        Assert.False(s.Snapshot().CanChangeNoiseMode);
        var current=s.BeginConnection();s.ChannelOpened(current);Assert.Null(s.Snapshot().ReportedNoiseMode);
        Assert.False(s.Observe(old,Report(0x0d,3)));Assert.Null(s.Snapshot().ReportedNoiseMode);
        Assert.False(s.TryRequestMode(old,NoiseMode.Off,out _));
        s.Observe(current,Report(0x0d,2));Assert.True(s.Snapshot().CanChangeNoiseMode);
    }
    [Fact]
    public void UnknownFramesDoNotSupplyReadinessOrExportPrivateMetadata()
    {
        var s=new AapControlState();long e=s.BeginConnection();s.ChannelOpened(e);
        byte[] metadata=[4,0,4,0,0x1d,0,65,73,82,80,79,68,83];
        Assert.False(s.Observe(e,metadata));
        Assert.Equal(ControlPhase.AwaitingReports,s.Snapshot().Phase);
        Assert.Equal(1,s.Snapshot().UnrecognizedPackets);
        Assert.DoesNotContain("AIRPODS",s.ExportJson());
        Assert.Contains("aap-control",s.ExportJson());
    }
    private sealed class Transport : IAapTransport
    {
        public readonly List<byte[]> Writes=[];
        public readonly Channel<ReadOnlyMemory<byte>> Input=Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        public bool Disposed;
        public ValueTask ConnectAsync(CancellationToken token)=>ValueTask.CompletedTask;
        public ValueTask SendAsync(ReadOnlyMemory<byte> p,CancellationToken token) {lock(Writes)Writes.Add(p.ToArray());return ValueTask.CompletedTask;}
        public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken token)=>Input.Reader.ReadAsync(token);
        public ValueTask DisposeAsync(){Disposed=true;Input.Writer.TryComplete();return ValueTask.CompletedTask;}
    }
    [Fact]
    public async Task ExternalCancellationClosesTransportAndDisablesControls()
    {
        var t=new Transport();await using var c=new AapController(t);
        using var stop=new CancellationTokenSource();await c.ConnectAsync(stop.Token);
        stop.Cancel();using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while(!t.Disposed) await Task.Delay(1,timeout.Token);
        Assert.Equal(ControlPhase.Disconnected,c.State.Snapshot().Phase);
        Assert.False(c.State.Snapshot().CanChangeNoiseMode);
        Assert.Equal(3,t.Writes.Count);
    }
    [Fact]
    public async Task ReceiveFailureClosesTransportAndRequiresANewExplicitSession()
    {
        var t=new Transport();await using var c=new AapController(t);
        await c.ConnectAsync();t.Input.Writer.TryComplete(new IOException("Synthetic transport loss"));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while(!t.Disposed) await Task.Delay(1,timeout.Token);
        Assert.Equal(ControlPhase.Failed,c.State.Snapshot().Phase);
        Assert.Equal("aap-receive",c.State.Snapshot().Issue!.Stage);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>c.ConnectAsync());
        Assert.Equal(3,t.Writes.Count);
    }
    [Fact]
    public async Task ControllerSubscribesBeforeCommandsAndStopsWithoutReplayingWrites()
    {
        var t=new Transport();await using var c=new AapController(t);
        await c.ConnectAsync();Assert.Equal(3,t.Writes.Count);
        Assert.Equal(AapProtocol.Handshake(),t.Writes[0]);Assert.Equal(AapProtocol.Subscribe(),t.Writes[2]);
        await t.Input.Writer.WriteAsync(Report(0x0d,2));
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(3));
        while(!c.State.Snapshot().CanChangeNoiseMode)await Task.Delay(1,timeout.Token);
        await c.SetModeAsync(NoiseMode.Transparency);
        Assert.Equal(CommandOutcome.Pending,c.State.Snapshot().LastCommandOutcome);
        Assert.Equal(4,t.Writes.Count);
        await c.DisposeAsync();Assert.True(t.Disposed);
        Assert.Equal(ControlPhase.Disconnected,c.State.Snapshot().Phase);
        Assert.Equal(4,t.Writes.Count);
    }
}
