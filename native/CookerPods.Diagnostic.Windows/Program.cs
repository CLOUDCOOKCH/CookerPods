using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using CookerPods.Diagnostic.Core;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace CookerPods.Diagnostic.Windows;

internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        if (args.Contains("--help")) { Help(); return 0; }
        int seconds = 30;
        bool allPaired = false, noWait = false;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--no-wait") noWait = true;
            else if (args[i] == "--all-paired") allPaired = true;
            else if (args[i] == "--seconds" && i + 1 < args.Length && int.TryParse(args[++i], out int n) && n is >= 10 and <= 120) seconds = n;
            else { Help(); Console.Error.WriteLine("Invalid arguments. Scan duration must be between 10 and 120 seconds."); return 2; }
        }
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("The hardware diagnostic requires Windows. No Bluetooth operation was attempted.");
            return 2;
        }
        Console.WriteLine("CookerPods Windows connection lab — native preview 0.4.1");
        Console.WriteLine("Read-only. Standard Windows APIs; no driver changes, administrator request, settings writes, or network listener.");
        Console.WriteLine("Reports exclude names, addresses, device IDs, and raw broadcasts. Nothing is uploaded.");
        Console.WriteLine("Paired device names are shown in this console only. Nearby candidates are NOT verified as your AirPods.\n");
        using var canceled = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, e) => { e.Cancel = true; canceled.Cancel(); };
        Console.CancelKeyPress += cancelHandler;
        var report = new DiagnosticReport
        {
            WindowsVersion = Environment.OSVersion.Version.ToString(),
            ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            OsArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            RequestedScanSeconds = seconds
        };
        int exit = 0;
        try
        {
            await InspectAdapter(report, canceled.Token);
            await InspectPairedDevices(report, allPaired, canceled.Token);
            if (!canceled.IsCancellationRequested) await ObserveBroadcasts(report, seconds, canceled.Token);
        }
        catch (OperationCanceledException) { report.Canceled = true; }
        catch (Exception error) { report.Issues.Add(DiagnosticIssue.From("diagnostic", error)); exit = 1; }
        finally { Console.CancelKeyPress -= cancelHandler; }
        report.Canceled |= canceled.IsCancellationRequested;
        report.FinishedAtUtc = DateTimeOffset.UtcNow;
        if (report.Observations is { } observations)
        {
            Console.WriteLine($"\nReceived {observations.AppleManufacturerSections} Apple manufacturer section(s), {observations.Candidates.Count} proximity candidate(s).");
            foreach (var candidate in observations.Candidates)
            {
                Console.WriteLine($"{candidate.Label}: {candidate.Observations} observation(s), last seen {candidate.LastSeenUtc:HH:mm:ss} UTC; identity UNASSOCIATED");
                if (candidate.ExperimentalEstimate is { } estimate)
                    Console.WriteLine($"  Experimental: model code {estimate.ModelCode}; slot A {Percent(estimate.EarbudSlotAPercent)}, slot B {Percent(estimate.EarbudSlotBPercent)}, case {Percent(estimate.CasePercent)}. Left/right mapping unverified.");
                else Console.WriteLine($"  Latest frame is unknown: {candidate.LatestDecodeOutcome}. No current battery value is inferred.");
                Console.WriteLine($"  First-check outcomes: {string.Join(", ", candidate.DecodeOutcomes.Select(p => $"{p.Key}={p.Value}"))}.");
                if (candidate.LastDecodedEstimate is { } historical)
                {
                    var value = historical.Estimate;
                    double age = Math.Max(0, (report.FinishedAtUtc!.Value - historical.ObservedAtUtc).TotalSeconds);
                    Console.WriteLine($"  Most recent decoded sample: {historical.ObservedAtUtc:O} ({age:0.0}s old at scan end; may be stale).");
                    Console.WriteLine($"  Historical slot A {Percent(value.EarbudSlotAPercent)}, slot B {Percent(value.EarbudSlotBPercent)}, case {Percent(value.CasePercent)}; identity and left/right mapping unverified.");
                }
            }
            if (observations.Candidates.Count == 0)
                Console.WriteLine("No proximity candidate received. This is inconclusive; the AirPods may not be broadcasting. It is not a zero-battery result.");
        }
        foreach (var issue in report.Issues) Console.WriteLine($"Issue: {issue.Stage}: {issue.Kind} ({issue.ErrorCode}). No elevation is suggested as a workaround.");
        try
        {
            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CookerPods", "reports");
            Directory.CreateDirectory(directory);
            string filename = $"native-report-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
            string destination = Path.Combine(directory, filename);
            await File.WriteAllTextAsync(destination, report.ToJson());
            Console.WriteLine($"\nReport saved locally:\n{destination}\nInspect the JSON before sharing it.");
        }
        catch (Exception error)
        {
            exit = 1;
            Console.Error.WriteLine($"Report could not be saved ({error.GetType().Name}).");
        }
        Console.WriteLine("\nThis diagnostic does not establish a working AirPods manager or control transport.");
        if (!noWait && !Console.IsInputRedirected) { Console.WriteLine("Press Enter to close."); Console.ReadLine(); }
        return exit;
    }

    private static string Percent(int? value) => value is { } n ? $"about {n}%" : "unavailable";

    private static void Help() => Console.WriteLine("CookerPods-Diagnostic [--seconds 10..120] [--all-paired] [--no-wait]\nDefault: 30-second passive scan; three paired-device queries with names shown locally only for AirPods matches.\n--all-paired shows other paired names locally; no names are exported. No settings writes or network API.");

    private static async Task InspectAdapter(DiagnosticReport report, CancellationToken token)
    {
        try
        {
            var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10), token);
            report.Adapter = adapter is null ? new(false, null, null) : new(true, adapter.IsLowEnergySupported, adapter.IsCentralRoleSupported);
            Console.WriteLine(adapter is null ? "No default Bluetooth adapter was returned." : $"Default adapter found. BLE support: {adapter.IsLowEnergySupported}; central role: {adapter.IsCentralRoleSupported}.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { report.Issues.Add(DiagnosticIssue.From("adapter", error)); }
    }

    private static bool NameSuggestsAirPods(string name) => name.Contains("AirPods", StringComparison.OrdinalIgnoreCase);

    private static async Task InspectPairedDevices(DiagnosticReport report, bool allPaired, CancellationToken token)
    {
        await InspectPairedQuery(report, allPaired, "ClassicDeviceInterface",
            BluetoothDevice.GetDeviceSelectorFromPairingState(true), false, token);
        // Explicit endpoint queries supplement the default device-interface view.
        // Protocol IDs and DeviceInformationKind are documented by Microsoft.
        foreach (var (source, protocol) in new[]
        {
            ("ClassicAssociationEndpoint", "{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}"),
            ("LowEnergyAssociationEndpoint", "{bb7bb05e-5972-42b5-94fc-76eaa7084d49}")
        })
        {
            string selector = $"System.Devices.Aep.ProtocolId:=\"{protocol}\" AND System.Devices.Aep.IsPaired:=System.StructuredQueryType.Boolean#True";
            await InspectPairedQuery(report, allPaired, source, selector, true, token);
        }
        Console.WriteLine("Paired queries can overlap; their counts are not unique devices or confirmation of audio routing.");
        Console.WriteLine("Every returned entry is reported anonymously (up to 32 per query). Use --all-paired to show other names locally.");
    }

    private static async Task InspectPairedQuery(DiagnosticReport report, bool allPaired,
        string source, string selector, bool endpoints, CancellationToken token)
    {
        try
        {
            var devices = endpoints
                ? await DeviceInformation.FindAllAsync(selector, ["System.Devices.Aep.IsConnected"],
                    DeviceInformationKind.AssociationEndpoint).AsTask().WaitAsync(TimeSpan.FromSeconds(10), token)
                : await DeviceInformation.FindAllAsync(selector).AsTask().WaitAsync(TimeSpan.FromSeconds(10), token);
            if (!endpoints) report.PairedDevicesEnumerated = devices.Count;
            int matches = devices.Count(d => NameSuggestsAirPods(d.Name));
            var retained = devices.OrderByDescending(d => NameSuggestsAirPods(d.Name)).Take(32).ToArray();
            report.PairedEnumeration.Add(new(source, devices.Count, retained.Length, matches, devices.Count > retained.Length));
            Console.WriteLine($"{source}: {devices.Count} paired entry/entries; {matches} name match(es); {retained.Length} reported.");
            int ordinal = 0, statusReads = 0;
            foreach (var info in retained)
            {
                token.ThrowIfCancellationRequested();
                bool suggestsAirPods = NameSuggestsAirPods(info.Name);
                string label = $"{source} candidate {++ordinal}";
                string status = "NotRead", statusSource = "NameFilterOrReadLimit";
                if (endpoints)
                {
                    status = info.Properties.TryGetValue("System.Devices.Aep.IsConnected", out var value) && value is bool connected
                        ? connected ? "Connected" : "Disconnected" : "NotReported";
                    statusSource = "System.Devices.Aep.IsConnected";
                }
                else if (suggestsAirPods && statusReads++ < 4)
                {
                    statusSource = "BluetoothDevice.ConnectionStatus";
                    try
                    {
                        using var device = await BluetoothDevice.FromIdAsync(info.Id).AsTask().WaitAsync(TimeSpan.FromSeconds(5), token);
                        status = device?.ConnectionStatus.ToString() ?? "AccessUnavailable";
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception error) { report.Issues.Add(DiagnosticIssue.From("paired-device-" + source, error)); }
                }
                // Include unmatched entries anonymously: a name filter must not
                // turn an enumerated device into an unexplained empty report.
                report.PairedDevices.Add(new(label, suggestsAirPods, status, source, statusSource));
                if (allPaired || suggestsAirPods)
                {
                    string localName = new(info.Name.Where(c => !char.IsControl(c)).Take(100).ToArray());
                    Console.WriteLine($"  {label}: {localName} — OS connection: {status}. Name matching is a heuristic.");
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { report.Issues.Add(DiagnosticIssue.From("paired-enumeration-" + source, error)); }
    }

    private static async Task ObserveBroadcasts(DiagnosticReport report, int seconds, CancellationToken token)
    {
        var collector = new ObservationCollector();
        var ended = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Passive };
        int callbackFailures = 0;
        void Received(BluetoothLEAdvertisementWatcher _, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            try
            {
                foreach (var section in args.Advertisement.ManufacturerData)
                {
                    if (section.CompanyId != 0x004c) continue;
                    if (section.Data.Length > 512) { collector.Observe(args.BluetoothAddress, new byte[513], args.Timestamp); continue; }
                    byte[] payload = new byte[section.Data.Length];
                    using var reader = DataReader.FromBuffer(section.Data);
                    reader.ReadBytes(payload);
                    collector.Observe(args.BluetoothAddress, payload, args.Timestamp);
                }
            }
            catch { Interlocked.Increment(ref callbackFailures); }
        }
        void Stopped(BluetoothLEAdvertisementWatcher _, BluetoothLEAdvertisementWatcherStoppedEventArgs args) => ended.TrySetResult(args.Error.ToString());
        watcher.Received += Received;
        watcher.Stopped += Stopped;
        var elapsed = Stopwatch.StartNew();
        try
        {
            Console.WriteLine($"\nObserving broadcasts for up to {seconds} seconds. Open your AirPods case nearby; leave Windows pairing intact. Ctrl+C ends the scan.");
            watcher.Start();
            report.ScanStarted = true;
            await Task.WhenAny(Task.Delay(TimeSpan.FromSeconds(seconds), token), ended.Task);
            report.WatcherStopReason = ended.Task.IsCompletedSuccessfully ? ended.Task.Result
                : token.IsCancellationRequested ? "UserCanceled" : "DurationElapsed";
            Console.WriteLine($"Watcher finished: {report.WatcherStopReason}.");
        }
        catch (Exception error) { report.Issues.Add(DiagnosticIssue.From("advertisement-watcher", error)); }
        finally
        {
            report.Observations = collector.Snapshot(close: true);
            try { watcher.Stop(); } catch (Exception error) { report.Issues.Add(DiagnosticIssue.From("watcher-stop", error)); }
            watcher.Received -= Received;
            watcher.Stopped -= Stopped;
            report.ActualScanSeconds = Math.Round(elapsed.Elapsed.TotalSeconds, 2);
            if (callbackFailures > 0) report.Issues.Add(new("advertisement-callback", "ReadFailure", $"count:{callbackFailures}"));
        }
    }
}
