using System.Text.Json;
using System.Text.Json.Serialization;

namespace CookerPods.Diagnostic.Core;

[JsonConverter(typeof(JsonStringEnumConverter<MonitorReadingState>))]
public enum MonitorReadingState { ChooseCandidate, Waiting, Live, Stale, Paused, SelectionExpired, ScanUnavailable }

public sealed record LiveMonitorSnapshot(DateTimeOffset CapturedAtUtc, bool Scanning,
    MonitorReadingState State, string? SelectedCandidateLabel, NearbyCandidate? SelectedCandidate,
    TimestampedEstimate? DisplaySample, bool IsFresh, double? SampleAgeSeconds,
    ScanObservations Observations, DiagnosticIssue? ScanIssue, IReadOnlyList<string> CurrentRunCandidates);

public sealed class LiveMonitorSession(TimeProvider? timeProvider = null, int capacity = 32) : IDisposable
{
    public static readonly TimeSpan FreshFor = TimeSpan.FromSeconds(15);
    public static readonly TimeSpan RetainFor = TimeSpan.FromMinutes(2);
    private readonly TimeProvider clock = timeProvider ?? TimeProvider.System;
    private readonly object gate = new();
    private readonly ObservationCollector collector = new(capacity, evictOldest: true);
    private readonly HashSet<string> observedThisRun = [];
    private long generation;
    private bool scanning, disposed, selectionExpired;
    private string? selected;
    private DiagnosticIssue? scanIssue;

    public long BeginScan()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            scanning = true;
            scanIssue = null;
            observedThisRun.Clear();
            return ++generation;
        }
    }

    public void Pause()
    {
        lock (gate) { scanning = false; generation++; }
    }

    public void ScanStopped(long epoch, DiagnosticIssue? issue)
    {
        lock (gate)
        {
            if (disposed || generation != epoch) return;
            scanning = false;
            scanIssue = issue;
            generation++;
        }
    }

    public void Observe(long epoch, ulong privateRadioAddress, ReadOnlySpan<byte> payload)
    {
        lock (gate)
        {
            if (disposed || !scanning || generation != epoch) return;
            var now = clock.GetUtcNow();
            collector.PruneBefore(now - RetainFor);
            var label = collector.Observe(privateRadioAddress, payload, now);
            if (label is not null)
            {
                if (observedThisRun.Count >= capacity)
                    observedThisRun.IntersectWith(collector.Snapshot().Candidates.Select(c => c.Label));
                observedThisRun.Add(label);
            }
        }
    }

    public bool SelectCandidate(string label)
    {
        lock (gate)
        {
            if (disposed) return false;
            var now = clock.GetUtcNow();
            collector.PruneBefore(now - RetainFor);
            var candidate = collector.Snapshot().Candidates.FirstOrDefault(c => c.Label == label);
            if (!scanning || !observedThisRun.Contains(label) || candidate?.ExperimentalEstimate is not { } estimate || !HasKnownBattery(estimate)
                || !FreshAge(now - candidate.LastSeenUtc)) return false;
            selected = label;
            selectionExpired = false;
            return true;
        }
    }

    public void ClearSelection()
    {
        lock (gate) { selected = null; selectionExpired = false; }
    }

    public LiveMonitorSnapshot Snapshot()
    {
        lock (gate)
        {
            var now = clock.GetUtcNow();
            collector.PruneBefore(now - RetainFor);
            var observations = collector.Snapshot();
            var candidate = observations.Candidates.FirstOrDefault(c => c.Label == selected);
            if (selected is not null && candidate is null)
            {
                selected = null;
                selectionExpired = true;
            }
            var sample = candidate?.LastDecodedEstimate;
            var age = sample is null ? (TimeSpan?)null : now - sample.ObservedAtUtc;
            bool fresh = scanning && candidate is not null && observedThisRun.Contains(candidate.Label)
                && candidate.ExperimentalEstimate is { } latest && HasKnownBattery(latest)
                && age is { } elapsed && FreshAge(elapsed);
            var state = scanIssue is not null ? MonitorReadingState.ScanUnavailable
                : !scanning ? MonitorReadingState.Paused
                : selectionExpired ? MonitorReadingState.SelectionExpired
                : candidate is null ? MonitorReadingState.ChooseCandidate
                : fresh ? MonitorReadingState.Live
                : sample is null ? MonitorReadingState.Waiting : MonitorReadingState.Stale;
            return new(now, scanning, state, selected, candidate, sample, fresh,
                age is { } a ? Math.Max(0, Math.Round(a.TotalSeconds, 1)) : null, observations, scanIssue,
                observedThisRun.Where(label => observations.Candidates.Any(c => c.Label == label)).ToArray());
        }
    }

    public static bool HasKnownBattery(ProximityEstimate estimate) =>
        estimate.EarbudSlotAPercent.HasValue || estimate.EarbudSlotBPercent.HasValue || estimate.CasePercent.HasValue;

    private static bool FreshAge(TimeSpan age) => age >= TimeSpan.FromSeconds(-2) && age <= FreshFor;

    public string ExportJson()
    {
        var snapshot = Snapshot();
        return JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            reportKind = "live-monitor",
            probeVersion = NativeBuild.Version,
            snapshot,
            limitations = new[]
            {
                "Candidate selection is user-directed and does not authenticate identity or link to Windows pairing.",
                "Slot A/B are not verified physical left/right roles. Battery estimates are approximate ten-point values.",
                "Nonzero undefined suffixes remain explicitly flagged; firmware layout is unverified.",
                "Freshness expires after 15 seconds. Historical samples are not current readings.",
                "Rotating radio addresses require a new explicit selection; no automatic device reassociation.",
                "No device controls, settings writes, server, or automatic uploads. No names, addresses, IDs or raw payloads exported."
            }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true });
    }

    public void Dispose()
    {
        lock (gate)
        {
            scanning = false;
            disposed = true;
            generation++;
            selected = null;
            observedThisRun.Clear();
            collector.Snapshot(close: true);
        }
    }
}
