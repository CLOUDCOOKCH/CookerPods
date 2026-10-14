namespace CookerPods.Diagnostic.Core;

public sealed record NearbyCandidate(string Label, int Observations, DateTimeOffset LastSeenUtc,
    int PayloadLength, ProximityEstimate? ExperimentalEstimate)
{
    public string Identity => "Unassociated nearby Apple proximity broadcast; not verified as your paired AirPods";
}

public sealed record ScanObservations(int AppleManufacturerSections, int IgnoredOversizedSections,
    int DroppedNewCandidates, IReadOnlyList<NearbyCandidate> Candidates);

public sealed class ObservationCollector(int capacity = 32)
{
    private readonly object gate = new();
    // Addresses are used only as transient radio keys. Never exposed by snapshots.
    private readonly Dictionary<ulong, NearbyCandidate> candidates = [];
    private int sections, oversized, dropped;
    private bool closed;
    private ScanObservations? frozen;

    public void Observe(ulong transientAddress, ReadOnlySpan<byte> payload, DateTimeOffset seen)
    {
        lock (gate)
        {
            if (closed) return;
            sections++;
            if (payload.Length > 512) { oversized++; return; }
            if (payload.Length == 0 || payload[0] != 0x07) return;
            if (!candidates.TryGetValue(transientAddress, out var current))
            {
                if (candidates.Count >= capacity) { dropped++; return; }
                current = new($"Nearby candidate {candidates.Count + 1}", 0, seen.ToUniversalTime(), payload.Length, null);
            }
            if (seen < current.LastSeenUtc)
            {
                candidates[transientAddress] = current with { Observations = current.Observations + 1 };
                return;
            }
            // A latest unknown format clears older estimates, rather than making
            // an old battery value look like a fresh decode of the unknown frame.
            candidates[transientAddress] = current with
            {
                Observations = current.Observations + 1,
                LastSeenUtc = seen.ToUniversalTime(),
                PayloadLength = payload.Length,
                ExperimentalEstimate = ProximityFrame.Decode(payload)
            };
        }
    }

    public ScanObservations Snapshot(bool close = false)
    {
        lock (gate)
        {
            if (frozen is not null) return frozen;
            var snapshot = new ScanObservations(sections, oversized, dropped, candidates.Values.ToArray());
            if (close)
            {
                closed = true;
                frozen = snapshot;
                candidates.Clear(); // Remove private radio keys once observation ends.
            }
            return snapshot;
        }
    }
}
