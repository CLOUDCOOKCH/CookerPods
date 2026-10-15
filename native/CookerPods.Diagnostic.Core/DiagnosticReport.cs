using System.Text.Json;

namespace CookerPods.Diagnostic.Core;

public sealed record DiagnosticIssue(string Stage, string Kind, string ErrorCode)
{
    public static DiagnosticIssue From(string stage, Exception exception) =>
        new(stage, exception.GetType().Name, $"0x{exception.HResult:X8}");
}

public sealed record PairedDeviceObservation(string Label, bool NameSuggestsAirPods, string ConnectionStatus,
    string Source, string ConnectionStatusSource);
public sealed record PairedEnumerationObservation(string Source, int Enumerated, int Reported,
    int NamesSuggestingAirPods, bool Truncated);
public sealed record AdapterObservation(bool Found, bool? LowEnergySupported, bool? CentralRoleSupported);

public sealed class DiagnosticReport
{
    public int SchemaVersion { get; } = 2;
    public string ProbeVersion { get; } = "0.4.1-native-preview";
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? FinishedAtUtc { get; set; }
    public string? WindowsVersion { get; init; }
    public string? ProcessArchitecture { get; init; }
    public string? OsArchitecture { get; init; }
    public int RequestedScanSeconds { get; init; }
    public double? ActualScanSeconds { get; set; }
    public bool ScanStarted { get; set; }
    public bool Canceled { get; set; }
    public string? WatcherStopReason { get; set; }
    public AdapterObservation? Adapter { get; set; }
    public int? PairedDevicesEnumerated { get; set; }
    public List<PairedDeviceObservation> PairedDevices { get; } = [];
    public List<PairedEnumerationObservation> PairedEnumeration { get; } = [];
    public List<DiagnosticIssue> Issues { get; } = [];
    public ScanObservations? Observations { get; set; }
    public string[] Limitations { get; } =
    [
        "Read-only native diagnostic. No settings writes, pairing, audio routing, or local/network API.",
        "Friendly-name matching is a heuristic; neither a paired name nor a broadcast proves exact model identity.",
        "Nearby broadcasts are not associated with paired devices; rotating addresses may create multiple candidates.",
        "Battery estimates are experimental 10-point plaintext fields, not verified left/right values or continuous telemetry.",
        "Decoder outcomes record the first failed check, not proof of encryption. Historical estimates retain their own timestamps and may be stale.",
        "Paired queries can overlap; counts must not be summed as unique physical devices. Endpoint connection properties do not establish audio routing.",
        "No AAP/L2CAP transport, ANC controls, ear automation, or encrypted-payload decryption is implemented.",
        "Names, addresses, Windows device identifiers, and raw/encrypted payloads are omitted. No automatic uploads."
    ];

    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    });
}
