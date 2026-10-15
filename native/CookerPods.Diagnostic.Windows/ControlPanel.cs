using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Automation;
using CookerPods.Diagnostic.Core;

namespace CookerPods.Diagnostic.Windows;

internal sealed class ControlPanel : StackPanel, IAsyncDisposable
{
    private readonly Func<IReadOnlyList<ControlDevice>> enumerate;
    private readonly Func<ControlDevice, IAapTransport> transport;
    private readonly ListBox services = new() { MinHeight = 44, MaxHeight = 88, BorderThickness = new Thickness(0) };
    private readonly TextBlock status = Text("Checking for the control driver…", 15);
    private readonly TextBlock reported = Text("Reported mode: unknown", 18);
    private readonly TextBlock result = Text("", 13);
    private readonly TextBlock battery = Text("Device-reported battery: unknown", 13);
    private readonly TextBlock presence = Text("Ear presence: unknown", 13);
    private readonly Button connect = Button("Connect controls"), refresh = Button("Refresh services");
    private readonly Dictionary<NoiseMode, Button> modes = [];
    private readonly Dictionary<AapSetting, (TextBlock Label, Button Action)> settings = [];
    private readonly CancellationTokenSource lifetime = new();
    private AapController? controller;
    private bool busy, disposed;
    internal Button ConnectButton => connect;
    internal IReadOnlyDictionary<NoiseMode, Button> ModeButtons => modes;
    internal Button SettingButton(AapSetting setting) => settings[setting].Action;
    internal AapControlSnapshot? Snapshot => controller?.State.Snapshot();

    internal ControlPanel(Func<IReadOnlyList<ControlDevice>>? enumerate = null,
        Func<ControlDevice, IAapTransport>? transport = null)
    {
        this.enumerate = enumerate ?? DriverAapTransport.Enumerate;
        this.transport = transport ?? (device => new DriverAapTransport(device));
        status.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        status.Margin = new Thickness(0, 0, 0, 10); Children.Add(status);
        services.SetResourceReference(BackgroundProperty, "Surface");
        services.SetResourceReference(Control.ForegroundProperty, "Text");
        ScrollViewer.SetHorizontalScrollBarVisibility(services, ScrollBarVisibility.Disabled);
        AutomationProperties.SetName(services, "Paired AirPods control services");
        Children.Add(services);
        var connection = new WrapPanel { Margin = new Thickness(0, 10, 0, 18) };
        connect.Margin = new Thickness(0, 0, 8, 6); refresh.Margin = new Thickness(0, 0, 0, 6);
        connection.Children.Add(connect); connection.Children.Add(refresh); Children.Add(connection);
        reported.Margin = new Thickness(0, 0, 0, 12); Children.Add(reported);
        var choices = new UniformGrid { Columns = 2 };
        foreach (NoiseMode mode in Enum.GetValues<NoiseMode>())
        {
            var action = Button(ModeName(mode)); action.Margin = new Thickness(0, 0, 8, 8);
            action.Click += async (_, _) => await RunChange(() => controller!.SetModeAsync(mode, lifetime.Token));
            modes[mode] = action; choices.Children.Add(action);
        }
        Children.Add(choices);
        result.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        result.Margin = new Thickness(0, 4, 0, 16); Children.Add(result);
        foreach (var setting in Enum.GetValues<AapSetting>())
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var label = Text(setting == AapSetting.EarDetection ? "Ear detection: unknown" : "Conversation awareness: unknown", 14);
            label.VerticalAlignment = VerticalAlignment.Center;
            var action = Button("Unavailable"); action.MinWidth = 100; action.Margin = new Thickness(10, 0, 0, 0);
            action.Click += async (_, _) =>
            {
                var s = controller?.State.Snapshot();
                bool? current = setting == AapSetting.EarDetection ? s?.ReportedEarDetection : s?.ReportedConversationAwareness;
                if (current.HasValue) await RunChange(() => controller!.SetSettingAsync(setting, !current.Value, lifetime.Token));
            };
            row.Children.Add(label); Grid.SetColumn(action, 1); row.Children.Add(action); Children.Add(row);
            settings[setting] = (label, action);
        }
        battery.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); battery.Margin = new Thickness(0, 12, 0, 6); Children.Add(battery);
        presence.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Children.Add(presence);
        var note = Text("Controls use the paired service chosen here, separately from the nearby battery candidate. Firmware compatibility and physical mode changes need hardware verification.", 12);
        note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); note.Margin = new Thickness(0, 16, 0, 0); Children.Add(note);
        connect.Click += async (_, _) => await ConnectOrDisconnect();
        refresh.Click += async (_, _) => await RefreshDevices();
        services.SelectionChanged += (_, _) => RefreshState();
        Loaded += async (_, _) => await RefreshDevices();
        RefreshState();
    }
    private static TextBlock Text(string text, double size) => new() { Text = text, FontSize = size, TextWrapping = TextWrapping.Wrap };
    private static Button Button(string text) => new() { Content = text, MinHeight = 44, Padding = new Thickness(12, 8, 12, 8) };
    internal static string ModeName(NoiseMode mode) => mode switch
    {
        NoiseMode.NoiseCancellation => "Noise cancellation", NoiseMode.Transparency => "Transparency",
        NoiseMode.Adaptive => "Adaptive", _ => "Off"
    };
    private async Task RefreshDevices()
    {
        if (busy || disposed || controller is not null) return;
        busy = true; RefreshState();
        try
        {
            var found = await Task.Run(enumerate, lifetime.Token);
            if (disposed) return;
            services.Items.Clear();
            foreach (var item in found) services.Items.Add(new ListBoxItem { Content = item.Label, Tag = item });
            if (found.Count == 1) services.SelectedIndex = 0;
            status.Text = found.Count == 0 ? "Control driver unavailable. Nearby battery monitoring still works." : "Choose the paired service, then connect.";
        }
        catch (OperationCanceledException) { }
        catch { if (!disposed) status.Text = "Control services could not be listed. Try Refresh services."; }
        finally { busy = false; if (!disposed) RefreshState(); }
    }
    private async Task ConnectOrDisconnect()
    {
        if (busy || disposed) return;
        if (controller is not null)
        {
            busy = true; RefreshState();
            try { await controller.DisposeAsync(); }
            catch { status.Text = "The control session ended with an error."; }
            finally { controller = null; busy = false; if (!disposed) RefreshState(); }
            return;
        }
        if (services.SelectedItem is not ListBoxItem { Tag: ControlDevice selected }) return;
        busy = true;
        controller = new AapController(transport(selected)); RefreshState();
        try { await controller.ConnectAsync(lifetime.Token); }
        catch { if (!disposed) status.Text = "Control connection failed. Disconnect, then try again."; }
        finally { busy = false; if (!disposed) RefreshState(); }
    }
    private async Task RunChange(Func<Task> change)
    {
        if (busy || disposed || controller is null) return;
        busy = true; RefreshState();
        try { await change(); }
        catch { if (!disposed) result.Text = "Change unconfirmed. Check the device-reported state before trying again."; }
        finally { busy = false; if (!disposed) RefreshState(); }
    }
    internal void RefreshState()
    {
        var s = controller?.State.Snapshot();
        connect.SetResourceReference(StyleProperty, controller is null ? (object)"PrimaryButton" : typeof(Button));
        connect.Content = controller is null ? "Connect controls" : "Disconnect controls";
        connect.IsEnabled = !busy && !disposed && (controller is not null || services.SelectedItem is not null);
        refresh.IsEnabled = !busy && !disposed && controller is null;
        services.IsEnabled = !busy && controller is null;
        services.Visibility = controller is null ? Visibility.Visible : Visibility.Collapsed;
        if (s is not null) status.Text = s.Phase switch
        {
            ControlPhase.Connecting => "Connecting to the paired control service…",
            ControlPhase.AwaitingReports => "Channel open — waiting for AirPods status",
            ControlPhase.Ready => "Receiving device-reported status",
            ControlPhase.Failed => "Control connection failed. Reported values are historical; disconnect, then try again.",
            _ => "Control session disconnected"
        };
        reported.Text = "Reported mode: " + (s?.ReportedNoiseMode is { } m ? ModeName(m) : "unknown")
            + (s is not null && s.Phase is ControlPhase.Disconnected or ControlPhase.Failed ? " (historical)" : "");
        foreach (var (mode, button) in modes)
        {
            button.SetResourceReference(StyleProperty, s?.ReportedNoiseMode == mode ? (object)"PrimaryButton" : typeof(Button));
            button.IsEnabled = !busy && !disposed && s?.CanRequestMode(mode) == true && s.ReportedNoiseMode != mode;
            AutomationProperties.SetHelpText(button, mode == NoiseMode.Adaptive && s?.AdaptiveReported != true ? "Available after the device has reported Adaptive mode in this connection." : "Requests this mode. The reported mode changes only after a device report.");
        }
        result.Text = s?.LastCommandOutcome switch
        {
            CommandOutcome.Pending => "Change requested — waiting for a matching device report…",
            CommandOutcome.DeviceReported => "Device reported the requested setting. Physical effect still needs verification.",
            CommandOutcome.Unconfirmed => "Change unconfirmed. Check the device-reported state before trying again.",
            CommandOutcome.Interrupted => "Connection interrupted; the change was not confirmed.",
            _ => "Choose a mode after the device reports its current setting."
        };
        foreach (var (setting, row) in settings)
        {
            bool? value = setting == AapSetting.EarDetection ? s?.ReportedEarDetection : s?.ReportedConversationAwareness;
            row.Label.Text = (setting == AapSetting.EarDetection ? "Ear detection: " : "Conversation awareness: ")
                + (value.HasValue ? value.Value ? "on" : "off" : "unknown");
            row.Action.Content = value.HasValue ? value.Value ? "Turn off" : "Turn on" : "Unavailable";
            row.Action.IsEnabled = !busy && !disposed && s?.CanChange(setting) == true;
        }
        battery.Text = s is null || s.Battery.IsEmpty ? "Device-reported battery: unknown" : "Device-reported battery: " +
            string.Join(" · ", s.Battery.Select(b => $"{b.Component} {(b.Percent.HasValue ? $"{b.Percent}%" : "unavailable")}"));
        presence.Text = $"Ear presence: primary {Presence(s?.PrimaryPresence)}, secondary {Presence(s?.SecondaryPresence)}";
    }
    private static string Presence(PodPresence? value) => value switch
    { PodPresence.InEar => "in ear", PodPresence.OutOfEar => "out of ear", PodPresence.InCase => "in case", _ => "unknown" };
    internal string ExportJson() => controller?.State.ExportJson() ?? new AapControlState().ExportJson();
    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true; await lifetime.CancelAsync();
        if (controller is not null) await controller.DisposeAsync();
        lifetime.Dispose();
    }
}
