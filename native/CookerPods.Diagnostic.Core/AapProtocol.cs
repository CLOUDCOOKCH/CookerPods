using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CookerPods.Diagnostic.Core;

[JsonConverter(typeof(JsonStringEnumConverter<NoiseMode>))]
public enum NoiseMode : byte { Off = 1, NoiseCancellation = 2, Transparency = 3, Adaptive = 4 }
[JsonConverter(typeof(JsonStringEnumConverter<AapSetting>))]
public enum AapSetting : byte { EarDetection = 0x0a, ConversationAwareness = 0x28 }
[JsonConverter(typeof(JsonStringEnumConverter<PodPresence>))]
public enum PodPresence : byte { InEar = 0, OutOfEar = 1, InCase = 2, Unknown = 255 }
[JsonConverter(typeof(JsonStringEnumConverter<BatteryChargeState>))]
public enum BatteryChargeState : byte { Unknown = 0, Charging = 1, Discharging = 2, Disconnected = 4 }
[JsonConverter(typeof(JsonStringEnumConverter<AapComponent>))]
public enum AapComponent : byte { Right = 2, Left = 4, Case = 8 }
public sealed record AapBattery(AapComponent Component, int? Percent, BatteryChargeState ChargeState);
public abstract record AapNotification;
public sealed record NoiseModeReport(NoiseMode Mode) : AapNotification;
public sealed record SettingReport(AapSetting Setting, bool Enabled) : AapNotification;
public sealed record BatteryReport(ImmutableArray<AapBattery> Components) : AapNotification;
public sealed record EarPresenceReport(PodPresence Primary, PodPresence Secondary) : AapNotification;

// Original implementation from documented protocol facts; no captured device
// identity/metadata or reference-project parser code is incorporated.
public static class AapProtocol
{
    public const int MaxPacketBytes = 2048;
    public static byte[] Handshake() => [0, 0, 4, 0, 1, 0, 2, 0, 0, 0, 0, 0, 0, 0, 0, 0];
    public static byte[] EnableFeatures() => [4, 0, 4, 0, 0x4d, 0, 0xff, 0, 0, 0, 0, 0, 0, 0];
    public static byte[] Subscribe() => [4, 0, 4, 0, 0x0f, 0, 0xff, 0xff, 0xfe, 0xff];

    public static byte[] SetNoiseMode(NoiseMode mode)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        return Control(0x0d, (byte)mode);
    }
    public static byte[] SetBoolean(AapSetting setting, bool enabled)
    {
        if (!Enum.IsDefined(setting)) throw new ArgumentOutOfRangeException(nameof(setting));
        return Control((byte)setting, enabled ? (byte)1 : (byte)2);
    }
    private static byte[] Control(byte setting, byte value) => [4, 0, 4, 0, 9, 0, setting, value, 0, 0, 0];

    public static bool TryDecode(ReadOnlySpan<byte> packet, out AapNotification? notification)
    {
        notification = null;
        if (packet.Length is < 6 or > MaxPacketBytes || (packet[0] != 4 || packet[1] != 0 || packet[2] != 4 || packet[3] != 0) || packet[5] != 0) return false;
        if (packet[4] == 9 && packet.Length == 11 && packet[8] == 0 && packet[9] == 0 && packet[10] == 0)
        {
            if (packet[6] == 0x0d && packet[7] is >= 1 and <= 4)
                notification = new NoiseModeReport((NoiseMode)packet[7]);
            else if ((packet[6] == (byte)AapSetting.EarDetection || packet[6] == (byte)AapSetting.ConversationAwareness)
                && packet[7] is 1 or 2)
                notification = new SettingReport((AapSetting)packet[6], packet[7] == 1);
        }
        else if (packet[4] == 6 && packet.Length == 8 && KnownPresence(packet[6]) && KnownPresence(packet[7]))
            notification = new EarPresenceReport((PodPresence)packet[6], (PodPresence)packet[7]);
        else if (packet[4] == 4 && packet.Length >= 7)
        {
            int count = packet[6];
            if (count is < 1 or > 3 || packet.Length != 7 + 5 * count) return false;
            var result = ImmutableArray.CreateBuilder<AapBattery>(count);
            int seen = 0;
            for (int i = 0; i < count; i++)
            {
                var entry = packet.Slice(7 + i * 5, 5);
                byte component = entry[0], percent = entry[2], state = entry[3];
                if (component is not (2 or 4 or 8) || (seen & component) != 0 || entry[1] != 1 || entry[4] != 1
                    || (percent > 100 && percent != 255) || state is not (0 or 1 or 2 or 4)) return false;
                seen |= component;
                result.Add(new((AapComponent)component, percent == 255 || state == 4 ? null : percent, (BatteryChargeState)state));
            }
            notification = new BatteryReport(result.MoveToImmutable());
        }
        return notification is not null;
    }
    private static bool KnownPresence(byte value) => value is 0 or 1 or 2 or 255;
}
