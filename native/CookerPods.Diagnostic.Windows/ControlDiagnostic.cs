using System.Runtime.InteropServices;
using System.Text.Json;
using CookerPods.Diagnostic.Core;

namespace CookerPods.Diagnostic.Windows;

internal static class ControlDiagnostic
{
    internal static async Task<int> RunAsync(string[] args)
    {
        int? selected = null;
        if (args.Length != 0)
        {
            if (args.Length != 2 || args[0] != "--connect-service" || !int.TryParse(args[1], out int index) || index is < 1 or > 8) return 2;
            selected = index;
        }
        var issues = new List<DiagnosticIssue>();
        int? count = null;
        bool opened = false;
        AapControlSnapshot? snapshot = null;
        try
        {
            var devices = DriverAapTransport.Enumerate(); count = devices.Count;
            if (selected is { } number)
            {
                if (number > devices.Count) throw new InvalidOperationException("Requested service is unavailable.");
                await using var controller = new AapController(new DriverAapTransport(devices[number - 1]));
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
                await controller.ConnectAsync(deadline.Token); opened = true;
                await Task.Delay(TimeSpan.FromSeconds(5), deadline.Token);
                snapshot = controller.State.Snapshot();
            }
        }
        catch (Exception error) { issues.Add(DiagnosticIssue.From("control-driver-diagnostic", error)); }
        Console.WriteLine(JsonSerializer.Serialize(new
        {
            schemaVersion = 1, reportKind = "control-driver-diagnostic", probeVersion = NativeBuild.Version,
            osArchitecture = RuntimeInformation.OSArchitecture.ToString(),
            processArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
            driverServicesEnumerated = count, sessionOpened = opened, snapshot, issues,
            scope = selected.HasValue ? "Explicit selected-service AAP initialization and status observation; no noise-mode or boolean-setting commands." : "Interface enumeration only; no device handle or AAP session opened.",
            limitations = new[] { "No names, paths, addresses or raw packets exported. No automatic upload or installation.", "An installed compatible signed driver is required for a control session. Enumeration is not hardware validation." }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true }));
        return issues.Count == 0 ? 0 : 1;
    }
}
