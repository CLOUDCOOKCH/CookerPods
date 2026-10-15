using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookerPods.Diagnostic.Core;

[JsonConverter(typeof(JsonStringEnumConverter<ControlPhase>))]
public enum ControlPhase { Disconnected, Connecting, AwaitingReports, Ready, Failed }
[JsonConverter(typeof(JsonStringEnumConverter<CommandOutcome>))]
public enum CommandOutcome { None, Pending, DeviceReported, Unconfirmed, Interrupted }
public sealed record PendingControl(byte Attribute, byte Value, DateTimeOffset RequestedAtUtc);
public sealed record AapControlSnapshot(ControlPhase Phase, NoiseMode? ReportedNoiseMode,
    bool? ReportedEarDetection, bool? ReportedConversationAwareness,
    ImmutableArray<AapBattery> Battery, PodPresence? PrimaryPresence, PodPresence? SecondaryPresence,
    DateTimeOffset? LastReportAtUtc, PendingControl? Pending, CommandOutcome LastCommandOutcome,
    DiagnosticIssue? Issue, long RecognizedReports, long UnrecognizedPackets, bool AdaptiveReported)
{
    public bool CanChangeNoiseMode => Phase == ControlPhase.Ready && ReportedNoiseMode.HasValue && Pending is null;
    public bool CanRequestMode(NoiseMode mode) => CanChangeNoiseMode && (mode != NoiseMode.Adaptive || AdaptiveReported);
    public bool CanChange(AapSetting setting) => Phase == ControlPhase.Ready && Pending is null &&
        (setting == AapSetting.EarDetection ? ReportedEarDetection :
            setting == AapSetting.ConversationAwareness ? ReportedConversationAwareness : null).HasValue;
}

public sealed class AapControlState(TimeProvider? timeProvider = null)
{
    private readonly object gate = new();
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private long epoch, reports, unrecognized;
    private ControlPhase phase;
    private NoiseMode? mode;
    private bool adaptiveReported;
    private bool? ears, conversation;
    private ImmutableArray<AapBattery> battery = [];
    private PodPresence? primary, secondary;
    private DateTimeOffset? lastReport;
    private PendingControl? pending;
    private long pendingTimestamp;
    private CommandOutcome outcome;
    private DiagnosticIssue? issue;

    public long BeginConnection()
    {
        lock (gate)
        {
            epoch++; phase = ControlPhase.Connecting;
            mode = null; adaptiveReported = false; ears = conversation = null; battery = [];
            primary = secondary = null; lastReport = null; pending = null;
            reports = unrecognized = 0; issue = null; outcome = CommandOutcome.None;
            return epoch;
        }
    }
    public void ChannelOpened(long generation)
    {
        lock (gate) { if (generation == epoch && phase == ControlPhase.Connecting) phase = ControlPhase.AwaitingReports; }
    }
    public void Disconnect(long generation, DiagnosticIssue? failure = null)
    {
        lock (gate)
        {
            if (generation != epoch) return;
            if (pending is not null) outcome = CommandOutcome.Interrupted;
            pending = null; issue = failure;
            phase = failure is null ? ControlPhase.Disconnected : ControlPhase.Failed;
            epoch++; // late packets/command completions cannot revive a closed session
        }
    }
    public bool Observe(long generation, ReadOnlySpan<byte> packet)
    {
        lock (gate)
        {
            if (generation != epoch || phase is not (ControlPhase.AwaitingReports or ControlPhase.Ready)) return false;
            ExpirePending();
            if (!AapProtocol.TryDecode(packet, out var notification)) { unrecognized++; return false; }
            reports++; lastReport = clock.GetUtcNow(); phase = ControlPhase.Ready;
            switch (notification)
            {
                case NoiseModeReport n:
                    mode = n.Mode; adaptiveReported |= n.Mode == NoiseMode.Adaptive; MatchPending(0x0d, (byte)n.Mode); break;
                case SettingReport f:
                    if (f.Setting == AapSetting.EarDetection) ears = f.Enabled; else conversation = f.Enabled;
                    MatchPending((byte)f.Setting, f.Enabled ? (byte)1 : (byte)2); break;
                case BatteryReport b: battery = b.Components; break;
                case EarPresenceReport e: primary = e.Primary; secondary = e.Secondary; break;
            }
            return true;
        }
    }
    public bool TryRequestMode(long generation, NoiseMode requested, out byte[] packet)
    {
        lock (gate)
        {
            packet = [];
            ExpirePending();
            if (!Enum.IsDefined(requested) || generation != epoch || !SnapshotInternal().CanRequestMode(requested) || mode == requested) return false;
            packet = AapProtocol.SetNoiseMode(requested);
            SetPending(0x0d, (byte)requested);
            return true;
        }
    }
    public bool TryRequestSetting(long generation, AapSetting setting, bool enabled, out byte[] packet)
    {
        lock (gate)
        {
            packet = [];
            ExpirePending();
            if (!Enum.IsDefined(setting) || generation != epoch || !SnapshotInternal().CanChange(setting)) return false;
            bool? current = setting == AapSetting.EarDetection ? ears : conversation;
            if (current == enabled) return false;
            packet = AapProtocol.SetBoolean(setting, enabled);
            SetPending((byte)setting, enabled ? (byte)1 : (byte)2);
            return true;
        }
    }
    public void WriteFailed(long generation, DiagnosticIssue failure)
    {
        lock (gate)
        {
            if (generation != epoch) return;
            pending = null; outcome = CommandOutcome.Unconfirmed; issue = failure;
        }
    }
    public AapControlSnapshot Snapshot()
    {
        lock (gate) { ExpirePending(); return SnapshotInternal(); }
    }
    private AapControlSnapshot SnapshotInternal() => new(phase, mode, ears, conversation, battery,
        primary, secondary, lastReport, pending, outcome, issue, reports, unrecognized, adaptiveReported);
    private void SetPending(byte attribute, byte value)
    {
        pending = new(attribute, value, clock.GetUtcNow());
        pendingTimestamp = clock.GetTimestamp(); outcome = CommandOutcome.Pending; issue = null;
    }
    private void MatchPending(byte attribute, byte value)
    {
        if (pending is not null && pending.Attribute == attribute && pending.Value == value)
        {
            pending = null; outcome = CommandOutcome.DeviceReported;
        }
    }
    private void ExpirePending()
    {
        if (pending is not null && clock.GetElapsedTime(pendingTimestamp) >= TimeSpan.FromSeconds(8))
        { pending = null; outcome = CommandOutcome.Unconfirmed; }
    }
    public string ExportJson() => JsonSerializer.Serialize(new
    {
        schemaVersion = 1, reportKind = "aap-control", probeVersion = NativeBuild.Version,
        capturedAtUtc = clock.GetUtcNow(), snapshot = Snapshot(), limitations = new[]
        {
            "Reported state comes from the selected driver's AAP channel, not nearby BLE candidate matching.",
            "A matching report is not independent proof of a physical mode change; verify on real hardware.",
            "Unknown settings remain unknown. Commands are not confirmation. No blind retries.",
            "No device paths, Bluetooth addresses, friendly names, identifiers or raw packets exported.",
            "Driver installation, signing and current-firmware hardware validation are separate prerequisites."
        }
    }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
}
