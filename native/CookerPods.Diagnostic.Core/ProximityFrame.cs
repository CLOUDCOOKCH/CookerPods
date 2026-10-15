using System.Text.Json.Serialization;

namespace CookerPods.Diagnostic.Core;

[JsonConverter(typeof(JsonStringEnumConverter<ProximityDecodeOutcome>))]
public enum ProximityDecodeOutcome
{
    Decoded, UnsupportedPayloadLength, NotProximityMessage, UnsupportedDeclaredLength,
    UnsupportedLayoutVersion, UnsupportedByte10, ReservedBatteryNibble
}

public sealed record ProximityDecodeResult(ProximityDecodeOutcome Outcome, ProximityEstimate? Estimate);

// Original implementation of the documented 0x07 / 25-byte Continuity format.
// Field provenance and uncertainty are recorded in docs/native-windows.md.
public sealed record ProximityEstimate(string ModelCode, int? EarbudSlotAPercent,
    int? EarbudSlotBPercent, int? CasePercent)
{
    public string Confidence => "Experimental 10-point estimates; physical device and left/right mapping unverified";
}

public static class ProximityFrame
{
    public static ProximityEstimate? Decode(ReadOnlySpan<byte> data) => Analyze(data).Estimate;

    public static ProximityDecodeResult Analyze(ReadOnlySpan<byte> data)
    {
        // Manufacturer company ID is supplied separately by Windows, not in data.
        // Other lengths/versions may exist; they are intentionally not guessed.
        if (data.Length != 27) return new(ProximityDecodeOutcome.UnsupportedPayloadLength, null);
        if (data[0] != 0x07) return new(ProximityDecodeOutcome.NotProximityMessage, null);
        if (data[1] != 25) return new(ProximityDecodeOutcome.UnsupportedDeclaredLength, null);
        if (data[2] != 1) return new(ProximityDecodeOutcome.UnsupportedLayoutVersion, null);
        // Conservative format gate, not a diagnosis of encryption.
        if (data[10] != 0) return new(ProximityDecodeOutcome.UnsupportedByte10, null);
        int a = data[6] & 15, b = data[6] >> 4, c = data[7] & 15;
        if (Invalid(a) || Invalid(b) || Invalid(c)) return new(ProximityDecodeOutcome.ReservedBatteryNibble, null);
        return new(ProximityDecodeOutcome.Decoded,
            new($"0x{data[3]:X2}{data[4]:X2}", Percent(a), Percent(b), Percent(c)));
    }

    private static bool Invalid(int nibble) => nibble is > 10 and < 15;
    private static int? Percent(int nibble) => nibble == 15 ? null : nibble * 10;
}
