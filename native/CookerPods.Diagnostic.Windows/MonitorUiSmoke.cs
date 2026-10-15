using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CookerPods.Diagnostic.Core;

namespace CookerPods.Diagnostic.Windows;

internal static class MonitorUiSmoke
{
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
        public override long GetTimestamp() => Now.UtcTicks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
    }
    private sealed class FixtureRadio(LiveMonitorSession session) : IMonitorRadio
    {
        public long Epoch { get; private set; }
        public void Start() => Epoch = session.BeginScan();
        public void Pause() => session.Pause();
        public void Dispose() => Pause();
        public void Emit(ulong address = 123, byte buds = 0xa9)
        {
            byte[] data = new byte[27];
            data[0] = 7; data[1] = 25; data[2] = 1; data[3] = 0x24; data[4] = 0x20;
            data[6] = buds; data[7] = 0x0f; data[10] = 0x80;
            session.Observe(Epoch, address, data);
        }
    }

    private sealed class FixtureControlTransport : IAapTransport
    {
        private readonly Channel<ReadOnlyMemory<byte>> input = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
        public int Writes { get; private set; }
        public ValueTask ConnectAsync(CancellationToken token)
        {
            Emit(0x0d, 2); Emit(0x0a, 1); Emit(0x28, 2);
            input.Writer.TryWrite(new byte[] {4,0,4,0,4,0,2, 4,1,83,2,1, 2,1,84,2,1});
            return ValueTask.CompletedTask;
        }
        public ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken token) { Writes++; return ValueTask.CompletedTask; }
        public ValueTask<ReadOnlyMemory<byte>> ReceiveAsync(CancellationToken token) => input.Reader.ReadAsync(token);
        public void Emit(byte attribute, byte value) => input.Writer.TryWrite(new byte[] {4,0,4,0,9,0,attribute,value,0,0,0});
        public ValueTask DisposeAsync() { input.Writer.TryComplete(); return ValueTask.CompletedTask; }
    }

    internal static int Run(string directory)
    {
        Directory.CreateDirectory(directory);
        var results = new List<object>();
        try
        {
            RenderOptions.ProcessRenderMode = System.Windows.Interop.RenderMode.SoftwareOnly;
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
            foreach (var (scene, theme, width, scale) in new[]
            {
                ("empty", "light", 700d, 1d), ("picker", "light", 700d, 1d), ("live", "dark", 700d, 1d),
                ("stale", "light", 700d, 1d), ("narrow", "dark", 480d, 2d),
                ("controls", "dark", 700d, 1d), ("pending", "light", 700d, 1d), ("controls-narrow", "dark", 480d, 2d)
            })
            {
                var clock = new Clock();
                using var session = new LiveMonitorSession(clock);
                using var radio = new FixtureRadio(session);
                bool controlScene = scene is "controls" or "pending" or "controls-narrow";
                var controlTransport = new FixtureControlTransport();
                var controls = controlScene ? new ControlPanel(() => [new("Synthetic paired control service", "synthetic-control-service")], _ => controlTransport, clock) : new ControlPanel(() => []);
                var window = new MonitorWindow(session, radio, testMode: true, themeOverride: theme, controls: controls)
                { Width = width, Height = 690 };
                window.Show();
                Settle(window);
                if (controlScene)
                {
                    window.ShowManagement();
                    PumpUntil(window, () => controls.ConnectButton.IsEnabled);
                    controls.ConnectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    PumpUntil(window, () => controls.Snapshot?.Phase == ControlPhase.Ready);
                    window.RefreshNow();
                    Require(!controls.ModeButtons[NoiseMode.Adaptive].IsEnabled, "Unreported Adaptive support must stay unavailable.");
                    controls.ModeButtons[NoiseMode.Transparency].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    PumpUntil(window, () => controls.Snapshot?.LastCommandOutcome == CommandOutcome.Pending);
                    Require(controls.Snapshot?.ReportedNoiseMode == NoiseMode.NoiseCancellation, "Sending a mode must not optimistically change its reported state.");
                    window.RefreshNow();
                    Require(controls.ChangeStatus.Contains("Requested: Transparency"), "Pending text must name the requested mode separately from reported state.");
                    if (scene != "pending")
                    {
                        controlTransport.Emit(0x0d, 3);
                        PumpUntil(window, () => controls.Snapshot?.LastCommandOutcome == CommandOutcome.DeviceReported);
                        window.RefreshNow();
                        controls.SettingButton(AapSetting.ConversationAwareness).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        PumpUntil(window, () => controls.Snapshot?.LastCommandOutcome == CommandOutcome.Pending);
                        Require(controls.Snapshot?.ReportedConversationAwareness == false, "A setting request must not change reported state.");
                        controlTransport.Emit(0x28, 1);
                        PumpUntil(window, () => controls.Snapshot?.ReportedConversationAwareness == true);
                    }
                    window.RefreshNow();
                    Require(UIElementAutomationPeer.CreatePeerForElement(controls.SettingButton(AapSetting.EarDetection))?.GetName().StartsWith("Ear detection:") == true,
                        "Ear detection action must identify its setting to UI Automation.");
                    Require(UIElementAutomationPeer.CreatePeerForElement(controls.SettingButton(AapSetting.ConversationAwareness))?.GetName().StartsWith("Conversation awareness:") == true,
                        "Conversation awareness action must identify its setting to UI Automation.");
                }
                else if (scene == "empty")
                {
                    Require(!window.ChooseButton.IsEnabled && !window.ReadingsVisible, "Empty state must not invent readings or a selection.");
                }
                else if (scene == "picker")
                {
                    radio.Emit(); radio.Emit(456, 0x77); window.RefreshNow(); Settle(window);
                    Require(!window.ChooseButton.IsEnabled && !window.ReadingsVisible, "Two candidates must require a deliberate row choice.");
                    window.Candidates.SelectedIndex = 0;
                    Require(window.ChooseButton.IsEnabled && session.Snapshot().SelectedCandidateLabel is null,
                        "Highlighting a candidate must enable the action without starting monitoring.");
                }
                else
                {
                    radio.Emit(); window.RefreshNow(); Settle(window);
                    Require(window.ChooseButton.IsEnabled, "A fresh synthetic candidate must be selectable.");
                    window.ChooseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Settle(window);
                    Require(window.ReadingsVisible && session.Snapshot().IsFresh, "Selection did not reveal the live reading.");
                    window.PauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(!session.Snapshot().Scanning && !session.Snapshot().IsFresh, "Pause did not stop claiming freshness.");
                    window.PauseButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(!session.Snapshot().IsFresh, "Resume must wait for a new packet.");
                    radio.Emit(); window.RefreshNow();
                    Require(session.Snapshot().IsFresh, "New packet after resume was not reflected.");
                    if (scene == "stale") { clock.Now = clock.Now.AddSeconds(16); window.RefreshNow(); }
                    Require(window.ReadingLabels.SequenceEqual(new[] { "~90%", "~100%", "—" }), "Incorrect displayed synthetic values or unavailable case.");
                    if (scene == "stale") Require(!session.Snapshot().IsFresh && window.StateLabel.Contains("Last estimate"), "Stale state not identified.");
                }
                Settle(window);
                string filename = $"monitor-{scene}-{theme}.png";
                var root = window.Root;
                Require(root.ActualWidth > 400 && root.ActualHeight > 400, "Window layout did not initialize.");
                var bitmap = new RenderTargetBitmap((int)Math.Ceiling(root.ActualWidth * scale),
                    (int)Math.Ceiling(root.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
                bitmap.Render(root);
                var encoder = new PngBitmapEncoder();
                var metadata = new BitmapMetadata("png");
                metadata.SetQuery("/tEXt/{str=Description}", "CookerPods WPF UI test capture; synthetic fixture, not AirPods hardware evidence.");
                encoder.Frames.Add(BitmapFrame.Create(bitmap, null, metadata, null));
                using (var file = File.Create(Path.Combine(directory, filename))) encoder.Save(file);
                results.Add(new { filename, theme, width, scale, synthetic = true, state = controlScene ? controls.Snapshot?.LastCommandOutcome.ToString() : session.Snapshot().State.ToString() });
                if (!controlScene && scene != "empty")
                {
                    window.ChangeButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(!window.ReadingsVisible && session.Snapshot().SelectedCandidateLabel is null, "Change candidate did not clear the selection.");
                }
                if (scene == "pending")
                {
                    controlTransport.Emit(0x0d, 3);
                    PumpUntil(window, () => controls.Snapshot?.LastCommandOutcome == CommandOutcome.DeviceReported);
                }
                if (controlScene)
                {
                    controls.ConnectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    PumpUntil(window, () => controls.Snapshot is null && controls.ConnectButton.IsEnabled);
                    Require(controls.ConnectionStatus == "Control session disconnected.", "Disconnect must stop claiming that device reports are being received.");
                    Require(controls.ModeButtons.Values.All(b => !b.IsEnabled), "Disconnected mode controls must be unavailable.");
                }
                window.Close();
                PumpUntil(window, () => !window.IsVisible);
            }
            app.Shutdown();
            File.WriteAllText(Path.Combine(directory, "ui-smoke-result.json"), JsonSerializer.Serialize(new
            {
                success = true, version = NativeBuild.Version, sourceCommit = Environment.GetEnvironmentVariable("GITHUB_SHA"),
                origin = "WPF RenderTargetBitmap captures of synthetic fixtures; client area only, no hardware validation", captures = results
            }, new JsonSerializerOptions { WriteIndented = true }));
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(Path.Combine(directory, "ui-smoke-result.json"), JsonSerializer.Serialize(new
            { success = false, kind = error.GetType().Name, message = error.Message }));
            Console.Error.WriteLine($"UI smoke failed: {error.GetType().Name}: {error.Message}");
            return 1;
        }
    }

    private static void PumpUntil(Window window, Func<bool> condition,
        [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(condition))] string? awaitedState = null)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            Settle(window);
            if (DateTime.UtcNow > deadline) throw new InvalidOperationException($"Timed out waiting for synthetic UI state: {awaitedState}.");
            Thread.Sleep(10);
        }
        Settle(window);
    }
    private static void Settle(Window window)
    {
        window.Dispatcher.Invoke(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
