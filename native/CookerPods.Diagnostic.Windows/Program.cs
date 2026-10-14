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
        Console.WriteLine("CookerPods Windows connection lab — native preview 0.4.0");
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
                else Console.WriteLine("  Battery format not recognized. No battery value is inferred.");
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

    private static void Help() => Console.WriteLine("CookerPods-Diagnostic [--seconds 10..120] [--all-paired] [--no-wait]\nDefault: 30-second passive scan, AirPods-named paired devices shown locally.\n--all-paired shows other paired names locally; no names are exported. No settings writes or network API.");

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

    private static async Task InspectPairedDevices(DiagnosticReport report, bool allPaired, CancellationToken token)
    {
        try
        {
            string selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
            var devices = await DeviceInformation.FindAllAsync(selector).AsTask().WaitAsync(TimeSpan.FromSeconds(10), token);
            report.PairedDevicesEnumerated = devices.Count;
            int shown = 0;
            foreach (var info in devices)
            {
                token.ThrowIfCancellationRequested();
                bool suggestsAirPods = info.Name.Contains("AirPods", StringComparison.OrdinalIgnoreCase);
                if (!allPaired && !suggestsAirPods) continue;
                string label = $"Paired candidate {++shown}";
                string status = "NotRead";
                try
                {
                    using var device = await BluetoothDevice.FromIdAsync(info.Id).AsTask().WaitAsync(TimeSpan.FromSeconds(5), token);
                    status = device?.ConnectionStatus.ToString() ?? "AccessUnavailable";
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error) { report.Issues.Add(DiagnosticIssue.From("paired-device", error)); }
                report.PairedDevices.Add(new(label, suggestsAirPods, status));
                // Strip console control characters; names never enter the report.
                string localName = new(info.Name.Where(c => !char.IsControl(c)).Take(100).ToArray());
                Console.WriteLine($"{label}: {localName} — OS connection: {status}. Name matching is a heuristic.");
            }
            if (shown == 0) Console.WriteLine("No paired friendly name contains AirPods. If renamed, run with --all-paired to inspect names locally.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception error) { report.Issues.Add(DiagnosticIssue.From("paired-enumeration", error)); }
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
