namespace CookerPods.Diagnostic.Core;

public sealed record TimestampedEstimate(DateTimeOffset ObservedAtUtc, ProximityEstimate Estimate);

public sealed record NearbyCandidate(string Label, int Observations, DateTimeOffset LastSeenUtc,
    int PayloadLength, ProximityEstimate? ExperimentalEstimate,
    ProximityDecodeOutcome LatestDecodeOutcome,
    IReadOnlyDictionary<ProximityDecodeOutcome, int> DecodeOutcomes,
    TimestampedEstimate? LastDecodedEstimate, IReadOnlyList<TimestampedEstimate> RecentDecodedEstimates)
{
    public string Identity => "Unassociated nearby Apple proximity broadcast; not verified as your paired AirPods";
}

public sealed record ScanObservations(int AppleManufacturerSections, int IgnoredOversizedSections,
    int DroppedNewCandidates, IReadOnlyList<NearbyCandidate> Candidates);

public sealed class ObservationCollector(int capacity = 32)
{
    public const int HistoryLimit = 8;
    private readonly object gate = new();
    // Addresses are private transient radio keys. No snapshot exposes them.
    private readonly Dictionary<ulong, CandidateState> candidates = [];
    private int sections, oversized, dropped;
    private ScanObservations? frozen;

    private sealed class CandidateState(string label)
    {
        public string Label { get; } = label;
        public int Count { get; set; }
        public DateTimeOffset LastSeen { get; set; } = DateTimeOffset.MinValue;
        public int Length { get; set; }
        public ProximityDecodeResult Latest { get; set; } = new(ProximityDecodeOutcome.UnsupportedPayloadLength, null);
        public Dictionary<ProximityDecodeOutcome, int> Outcomes { get; } = [];
        public List<TimestampedEstimate> History { get; } = [];

        public NearbyCandidate Snapshot() => new(Label, Count, LastSeen, Length, Latest.Estimate,
            Latest.Outcome, new Dictionary<ProximityDecodeOutcome, int>(Outcomes),
            History.LastOrDefault(), History.ToArray());
    }

    public void Observe(ulong transientAddress, ReadOnlySpan<byte> payload, DateTimeOffset seen)
    {
        lock (gate)
        {
            if (frozen is not null) return;
            sections++;
            if (payload.Length > 512) { oversized++; return; }
            if (payload.Length == 0 || payload[0] != 0x07) return;
            if (!candidates.TryGetValue(transientAddress, out var current))
            {
                if (candidates.Count >= capacity) { dropped++; return; }
                current = new($"Nearby candidate {candidates.Count + 1}");
                candidates.Add(transientAddress, current);
            }
            var result = ProximityFrame.Analyze(payload);
            current.Count++;
            current.Outcomes[result.Outcome] = current.Outcomes.GetValueOrDefault(result.Outcome) + 1;
            if (result.Estimate is { } estimate)
            {
                current.History.Add(new(seen.ToUniversalTime(), estimate));
                current.History.Sort((a, b) => a.ObservedAtUtc.CompareTo(b.ObservedAtUtc));
                if (current.History.Count > HistoryLimit) current.History.RemoveAt(0);
            }
            if (seen < current.LastSeen) return;
            // Unsupported latest data remains unknown. Historical estimates have
            // separate timestamps and are never presented as a fresh decode.
            current.LastSeen = seen.ToUniversalTime();
            current.Length = payload.Length;
            current.Latest = result;
        }
    }

    public ScanObservations Snapshot(bool close = false)
    {
        lock (gate)
        {
            if (frozen is not null) return frozen;
            var snapshot = new ScanObservations(sections, oversized, dropped,
                candidates.Values.Select(c => c.Snapshot()).ToArray());
            if (close)
            {
                frozen = snapshot;
                candidates.Clear();
            }
            return snapshot;
        }
    }
}
