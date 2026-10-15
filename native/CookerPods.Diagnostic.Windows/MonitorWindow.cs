using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Threading;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using CookerPods.Diagnostic.Core;
using Microsoft.Win32;

namespace CookerPods.Diagnostic.Windows;

internal sealed class MonitorWindow : Window
{
    private readonly LiveMonitorSession session;
    private readonly IMonitorRadio radio;
    private readonly bool testMode;
    private readonly string? themeOverride;
    private readonly ControlPanel controlPanel;
    private Expander? management;
    private bool closing, readyToClose;
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly Dictionary<string, CandidateRow> rows = [];
    private readonly StackPanel picker = new(), readings = new();
    private readonly ListBox candidateList = new() { MinHeight = 100, MaxHeight = 190, BorderThickness = new Thickness(0) };
    private readonly TextBlock titleText = Text("Battery monitor", 28, FontWeights.SemiBold);
    private readonly TextBlock empty = Text("Waiting for nearby broadcasts. Open the case, then take the earbuds out.", 15);
    private readonly TextBlock stateText = Text("Scanning for battery estimates", 16, FontWeights.SemiBold);
    private readonly TextBlock ageText = Text("", 13);
    private readonly TextBlock selectionText = Text("", 16, FontWeights.SemiBold);
    private readonly TextBlock detailText = Text("", 13);
    private readonly TextBlock feedback = Text("", 13);
    private readonly Button choose = Action("Monitor selected", primary: true);
    private readonly Button pause = Action("Pause scanning");
    private readonly Button change = Action("Change candidate");
    private readonly Button save = Action("Save report");
    private readonly BatteryView[] battery = [new("Slot A", false), new("Slot B", false), new("Case", true)];
    internal Grid Root { get; } = new();
    internal string StateLabel => stateText.Text;
    internal bool ReadingsVisible => readings.Visibility == Visibility.Visible;
    internal Button ChooseButton => choose;
    internal ListBox Candidates => candidateList;
    internal Button PauseButton => pause;
    internal Button ChangeButton => change;
    internal void ShowManagement() { if (management is not null) management.IsExpanded = true; }
    internal string[] ReadingLabels => battery.Select(b => b.Value.Text).ToArray();

    public MonitorWindow(LiveMonitorSession session, IMonitorRadio radio, bool testMode = false, string? themeOverride = null, ControlPanel? controls = null)
    {
        this.session = session;
        this.radio = radio;
        this.testMode = testMode;
        this.themeOverride = themeOverride;
        controlPanel = controls ?? (testMode ? new ControlPanel(() => []) : new ControlPanel());
        Title = "CookerPods";
        Width = 700; Height = 650; MinWidth = 480; MinHeight = 460;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FontFamily = new FontFamily("Segoe UI"); FontSize = 14;
        Icon = new DrawingImage(new GeometryDrawing(Brushes.Transparent, new Pen(new SolidColorBrush(Color.FromRgb(0, 102, 204)), 2),
            Geometry.Parse("M 3,4 Q 3,1 6,1 Q 9,1 9,4 L 9,13 Q 9,15 7,15 Q 5,15 5,13 L 5,6 M 13,4 Q 13,1 16,1 Q 19,1 19,4 L 19,13 Q 19,15 17,15 Q 15,15 15,13 L 15,6")));
        SetResourceReference(BackgroundProperty, "Background");
        SetResourceReference(ForegroundProperty, "Text");
        TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);
        InstallStyles();
        ApplyTheme();
        SourceInitialized += (_, _) => ApplyTheme();
        Content = Root;
        Root.SetResourceReference(Panel.BackgroundProperty, "Background");
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Root.RowDefinitions.Add(new RowDefinition());
        Root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        BuildHeader(); BuildBody(); BuildToolbar();
        AutomationProperties.SetLiveSetting(stateText, AutomationLiveSetting.Polite);
        AutomationProperties.SetName(candidateList, "Nearby broadcast candidates");
        ScrollViewer.SetHorizontalScrollBarVisibility(candidateList, ScrollBarVisibility.Disabled);
        candidateList.SelectionChanged += (_, _) => UpdateChoose();
        choose.Click += (_, _) =>
        {
            if (candidateList.SelectedItem is ListBoxItem { Tag: string label } && session.SelectCandidate(label))
            {
                feedback.Text = ""; RefreshNow(); change.Focus();
            }
            else feedback.Text = "That reading is no longer current. Wait for a new estimate and choose again.";
        };
        change.Click += (_, _) => { session.ClearSelection(); RefreshNow(); candidateList.Focus(); };
        pause.Click += (_, _) =>
        {
            if (session.Snapshot().Scanning) radio.Pause(); else radio.Start();
            RefreshNow();
        };
        save.Click += (_, _) => SaveReport();
        timer.Tick += (_, _) => RefreshNow();
        Loaded += (_, _) => { radio.Start(); RefreshNow(); if (!testMode) timer.Start(); };
        Closing += async (_, args) =>
        {
            if (readyToClose) return;
            args.Cancel = true;
            if (closing) return;
            closing = true; timer.Stop(); radio.Pause();
            try { await controlPanel.DisposeAsync(); }
            catch { /* Closing still releases the process-owned handle and stops observation. */ }
            finally { readyToClose = true; _ = Dispatcher.BeginInvoke((System.Action)Close); }
        };
        Closed += (_, _) =>
        {
            timer.Stop(); SystemEvents.UserPreferenceChanged -= PreferencesChanged;
            radio.Dispose(); session.Dispose();
        };
        if (!testMode) SystemEvents.UserPreferenceChanged += PreferencesChanged;
    }

    private static TextBlock Text(string value, double size = 14, FontWeight? weight = null) => new()
    {
        Text = value, FontSize = size, FontWeight = weight ?? FontWeights.Normal,
        TextWrapping = TextWrapping.Wrap
    };

    private static Button Action(string label, bool primary = false)
    {
        var button = new Button { Content = label, MinHeight = 44, Padding = new Thickness(14, 8, 14, 8) };
        button.SetResourceReference(StyleProperty, primary ? "PrimaryButton" : typeof(Button));
        return button;
    }

    private void BuildHeader()
    {
        var header = new Grid { Margin = new Thickness(24, 24, 24, 18) };
        header.ColumnDefinitions.Add(new ColumnDefinition());
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.Children.Add(titleText);
        var badge = new Border { CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 5, 10, 5),
            VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
        badge.SetResourceReference(Border.BackgroundProperty, "Soft");
        var label = Text("Experimental", 12, FontWeights.SemiBold); label.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        badge.Child = label; Grid.SetColumn(badge, 1); header.Children.Add(badge);
        Root.Children.Add(header);
    }

    private void BuildBody()
    {
        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, Padding = new Thickness(24, 0, 24, 12) };
        Grid.SetRow(scroll, 1); Root.Children.Add(scroll);
        var content = new StackPanel(); scroll.Content = content;
        var workspace = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(24) };
        workspace.SetResourceReference(Border.BackgroundProperty, "Surface");
        var inside = new StackPanel(); workspace.Child = inside; content.Children.Add(workspace);
        stateText.Margin = new Thickness(0, 0, 0, 4); inside.Children.Add(stateText);
        ageText.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        ageText.Margin = new Thickness(0, 0, 0, 22); inside.Children.Add(ageText);
        inside.Children.Add(picker); inside.Children.Add(readings);
        picker.Children.Add(Text("Choose the reading that matches your AirPods. Windows device names aren’t used.", 15));
        candidateList.Margin = new Thickness(0, 16, 0, 12);
        candidateList.SetResourceReference(Control.BackgroundProperty, "Surface");
        candidateList.SetResourceReference(Control.ForegroundProperty, "Text");
        picker.Children.Add(candidateList);
        empty.Margin = new Thickness(0, 4, 0, 18); empty.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        picker.Children.Add(empty);
        choose.HorizontalAlignment = HorizontalAlignment.Left; choose.IsEnabled = false;
        picker.Children.Add(choose);
        var selectionRow = new Grid { Margin = new Thickness(0, 0, 0, 20) };
        selectionRow.ColumnDefinitions.Add(new ColumnDefinition());
        selectionRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        selectionText.VerticalAlignment = VerticalAlignment.Center;
        selectionRow.Children.Add(selectionText);
        change.Margin = new Thickness(10, 0, 0, 0); Grid.SetColumn(change, 1); selectionRow.Children.Add(change);
        readings.Children.Add(selectionRow);
        var columns = new Grid();
        for (int i = 0; i < battery.Length; i++)
        {
            columns.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(battery[i].Root, i); columns.Children.Add(battery[i].Root);
        }
        readings.Children.Add(columns);
        var note = Text("Approximate 10-point estimates. Slot A/B mapping and device identity are unverified.", 13);
        note.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); note.Margin = new Thickness(0, 24, 0, 14);
        readings.Children.Add(note);

        feedback.SetResourceReference(TextBlock.ForegroundProperty, "Text"); feedback.Margin = new Thickness(0, 14, 0, 0);
        content.Children.Add(feedback);
        management = new Expander { Header = "Manage AirPods", Margin = new Thickness(0, 14, 0, 0) };
        management.SetResourceReference(Control.ForegroundProperty, "Text");
        var controlSurface = new Border { CornerRadius = new CornerRadius(16), Padding = new Thickness(24), Margin = new Thickness(0, 12, 0, 0), Child = controlPanel };
        controlSurface.SetResourceReference(Border.BackgroundProperty, "Surface");
        management.Content = controlSurface;
        management.Expanded += (_, _) => { workspace.Visibility = Visibility.Collapsed; titleText.Text = "AirPods controls"; controlPanel.RefreshState(); };
        management.Collapsed += (_, _) => { workspace.Visibility = Visibility.Visible; titleText.Text = "Battery monitor"; };
        content.Children.Add(management);
        var details = new Expander { Header = "Details and limitations", Margin = new Thickness(0, 14, 0, 0) };
        detailText.Text = "Select a nearby broadcast by comparing its estimates with a trusted reading. Selection does not verify ownership or Windows audio connection.";
        detailText.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); detailText.Margin = new Thickness(0, 10, 0, 0);
        details.SetResourceReference(Control.ForegroundProperty, "Text");
        details.Content = detailText; content.Children.Add(details);

    }

    private void BuildToolbar()
    {
        var toolbar = new Grid { Margin = new Thickness(24, 12, 24, 20) };
        toolbar.ColumnDefinitions.Add(new ColumnDefinition());
        toolbar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var actions = new StackPanel { Orientation = Orientation.Horizontal };
        pause.Margin = new Thickness(0, 0, 10, 0); actions.Children.Add(pause); actions.Children.Add(save);
        toolbar.Children.Add(actions);
        var privacy = Text(testMode ? "Synthetic fixture" : "Local only", 12); privacy.SetResourceReference(TextBlock.ForegroundProperty, "Muted");
        privacy.VerticalAlignment = VerticalAlignment.Center; privacy.Margin = new Thickness(12, 0, 0, 0);
        Grid.SetColumn(privacy, 1); toolbar.Children.Add(privacy);
        Grid.SetRow(toolbar, 2); Root.Children.Add(toolbar);
    }

    internal void RefreshNow()
    {
        var snapshot = session.Snapshot();
        controlPanel.RefreshState();
        UpdateCandidates(snapshot);
        picker.Visibility = snapshot.SelectedCandidate is null ? Visibility.Visible : Visibility.Collapsed;
        readings.Visibility = snapshot.SelectedCandidate is null ? Visibility.Collapsed : Visibility.Visible;
        pause.Content = snapshot.Scanning ? "Pause scanning" : "Resume scanning";
        string status = snapshot.State switch
        {
            MonitorReadingState.Live => "Fresh battery estimate",
            MonitorReadingState.Stale => "Last estimate — no current reading",
            MonitorReadingState.Paused => "Scanning paused",
            MonitorReadingState.ScanUnavailable => "Bluetooth scan unavailable",
            MonitorReadingState.SelectionExpired => "Choose the current broadcast again",
            MonitorReadingState.Waiting => "Waiting for a readable estimate",
            _ => "Choose a nearby broadcast"
        };
        if (stateText.Text != status)
        {
            stateText.Text = status;
            if (AutomationPeer.ListenerExists(AutomationEvents.LiveRegionChanged))
                UIElementAutomationPeer.CreatePeerForElement(stateText)?.RaiseAutomationEvent(AutomationEvents.LiveRegionChanged);
        }
        ageText.Text = snapshot.State == MonitorReadingState.ScanUnavailable
            ? "Check Bluetooth in Windows Settings, then resume scanning."
            : snapshot.SelectedCandidate is not null
                ? snapshot.IsFresh ? $"Updated {Age(snapshot.SampleAgeSeconds)}" : $"Historical sample: {Age(snapshot.SampleAgeSeconds)}. Values are not live."
                : snapshot.State == MonitorReadingState.SelectionExpired
                    ? "The old radio address expired. A similar candidate is not selected automatically."
                    : snapshot.Scanning ? "Open the case and take the earbuds out. Compare before choosing." : "Resume to receive new estimates.";
        selectionText.Text = snapshot.SelectedCandidateLabel ?? "";
        var estimate = snapshot.DisplaySample?.Estimate;
        battery[0].Set(estimate?.EarbudSlotAPercent, snapshot.IsFresh);
        battery[1].Set(estimate?.EarbudSlotBPercent, snapshot.IsFresh);
        battery[2].Set(estimate?.CasePercent, snapshot.IsFresh);
        string format = estimate is null ? "" : estimate.HasNonzeroSuffix
            ? " The undefined suffix differs from the historical example; firmware layout is unverified." : "";
        string issue = snapshot.ScanIssue is null ? "" : $" Scan status: {snapshot.ScanIssue.Kind} ({snapshot.ScanIssue.ErrorCode}).";
        detailText.Text = "Nearby broadcasts are not linked to Windows pairing or authenticated as yours. Slot A/B are not verified left/right roles. "
            + "A missing value means unavailable, never zero. Readings lose freshness after 15 seconds; candidates expire after two minutes. "
            + "Rotating radio addresses require another explicit selection. Pause stops observation; close this window to end the session. "
            + "No noise controls, charging flags, ear automation, network server, or automatic upload is implemented."
            + format + issue;
        UpdateChoose();
    }

    private void UpdateCandidates(LiveMonitorSnapshot snapshot)
    {
        var labels = snapshot.Observations.Candidates.Select(c => c.Label).ToHashSet();
        foreach (string id in rows.Keys.Where(id => !labels.Contains(id)).ToArray())
        {
            candidateList.Items.Remove(rows[id].Item); rows.Remove(id);
        }
        foreach (var candidate in snapshot.Observations.Candidates)
        {
            if (!rows.TryGetValue(candidate.Label, out var row))
            {
                row = new(candidate.Label); rows.Add(candidate.Label, row); candidateList.Items.Add(row.Item);
            }
            var estimate = candidate.ExperimentalEstimate;
            var age = snapshot.CapturedAtUtc - candidate.LastSeenUtc;
            row.CanSelect = snapshot.Scanning && snapshot.CurrentRunCandidates.Contains(candidate.Label) && estimate is not null && LiveMonitorSession.HasKnownBattery(estimate)
                && age >= TimeSpan.FromSeconds(-2) && age <= LiveMonitorSession.FreshFor;
            row.Summary.Text = estimate is null ? "No readable battery fields yet"
                : $"Slot A {Percent(estimate.EarbudSlotAPercent)}  ·  Slot B {Percent(estimate.EarbudSlotBPercent)}  ·  Case {Percent(estimate.CasePercent)}";
            row.Time.Text = $"Seen {Age(Math.Max(0, age.TotalSeconds))}";
            AutomationProperties.SetHelpText(row.Item, row.Summary.Text + ". " + row.Time.Text);
        }
        empty.Text = snapshot.Scanning ? "Waiting for nearby broadcasts. Open the case, then take the earbuds out." : "No current broadcast. Resume scanning to receive new estimates.";
        empty.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        candidateList.Visibility = rows.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        if (candidateList.SelectedItem is null && rows.Count == 1) candidateList.SelectedIndex = 0;
    }

    private void UpdateChoose() => choose.IsEnabled = candidateList.SelectedItem is ListBoxItem { Tag: string id }
        && rows.TryGetValue(id, out var row) && row.CanSelect;

    private void SaveReport()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Save a private CookerPods report", Filter = "JSON report (*.json)|*.json",
            FileName = $"CookerPods-monitor-{DateTime.Now:yyyyMMdd-HHmmss}.json", AddExtension = true
        };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            System.IO.File.WriteAllText(dialog.FileName, (management?.IsExpanded == true ? controlPanel.ExportJson() : session.ExportJson()));
            feedback.Text = "Report saved locally. Inspect it before sharing; names and radio addresses are excluded.";
        }
        catch { feedback.Text = "The report could not be saved. Choose a different folder and try again."; }
    }

    private void PreferencesChanged(object? sender, UserPreferenceChangedEventArgs args) => Dispatcher.BeginInvoke((System.Action)ApplyTheme);

    private void ApplyTheme()
    {
        bool dark = themeOverride == "dark";
        if (themeOverride is null)
        {
            try { dark = Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "AppsUseLightTheme", 1) is int value && value == 0; }
            catch { dark = false; }
        }
        var colors = dark
            ? new Dictionary<string, string> { ["Background"] = "#141416", ["Surface"] = "#202023", ["Text"] = "#f5f5f7", ["Muted"] = "#b0b0b9", ["Line"] = "#43434a", ["Soft"] = "#35353c", ["Accent"] = "#0066cc", ["AccentHover"] = "#005bb8", ["Focus"] = "#80b9ff" }
            : new Dictionary<string, string> { ["Background"] = "#f5f5f7", ["Surface"] = "#ffffff", ["Text"] = "#202124", ["Muted"] = "#62636a", ["Line"] = "#dcdce0", ["Soft"] = "#e9e9ee", ["Accent"] = "#0066cc", ["AccentHover"] = "#0055aa", ["Focus"] = "#0071e3" };
        foreach (var (key, value) in colors) Resources[key] = (SolidColorBrush)new BrushConverter().ConvertFromString(value)!;
        Resources["Bar"] = Resources[dark ? "Focus" : "Accent"];
        Resources["ButtonText"] = Brushes.White;
        if (SystemParameters.HighContrast)
        {
            Resources["Background"] = Resources["Surface"] = SystemColors.WindowBrush;
            Resources["Text"] = Resources["Muted"] = SystemColors.WindowTextBrush;
            Resources["Soft"] = SystemColors.ControlBrush;
            Resources["Line"] = SystemColors.WindowTextBrush;
            Resources["Accent"] = Resources["AccentHover"] = Resources["Focus"] = Resources["Bar"] = SystemColors.HighlightBrush;
            Resources["ButtonText"] = SystemColors.HighlightTextBrush;
        }
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            int enabled = dark && !SystemParameters.HighContrast ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);

    private void InstallStyles()
    {
        const string xaml = """
        <ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
          <Style TargetType="Button">
            <Setter Property="Background" Value="{DynamicResource Soft}"/><Setter Property="Foreground" Value="{DynamicResource Text}"/>
            <Setter Property="BorderBrush" Value="Transparent"/><Setter Property="BorderThickness" Value="3"/>
            <Setter Property="FontSize" Value="14"/><Setter Property="FontWeight" Value="SemiBold"/><Setter Property="Cursor" Value="Hand"/>
            <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="Button">
              <Border x:Name="Chrome" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="10" Padding="{TemplateBinding Padding}">
                <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" RecognizesAccessKey="True"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="{DynamicResource Line}"/></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="{DynamicResource Line}"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter Property="BorderBrush" Value="{DynamicResource Focus}"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.55"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style x:Key="PrimaryButton" TargetType="Button" BasedOn="{StaticResource {x:Type Button}}">
            <Setter Property="Background" Value="{DynamicResource Accent}"/><Setter Property="Foreground" Value="{DynamicResource ButtonText}"/>
            <Style.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter Property="Background" Value="{DynamicResource AccentHover}"/></Trigger><Trigger Property="IsPressed" Value="True"><Setter Property="Background" Value="{DynamicResource AccentHover}"/></Trigger></Style.Triggers>
          </Style>
          <Style TargetType="ScrollBar">
            <Setter Property="Width" Value="12"/><Setter Property="Background" Value="{DynamicResource Background}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
              <Border Background="{TemplateBinding Background}">
                <Track x:Name="PART_Track" Orientation="{TemplateBinding Orientation}" IsDirectionReversed="True" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" Value="{TemplateBinding Value}" ViewportSize="{TemplateBinding ViewportSize}">
                  <Track.DecreaseRepeatButton><RepeatButton Command="ScrollBar.PageUpCommand" Focusable="False" Opacity="0"/></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="{DynamicResource Line}" CornerRadius="5" Margin="3,2"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="ScrollBar.PageDownCommand" Focusable="False" Opacity="0"/></Track.IncreaseRepeatButton>
                </Track>
              </Border>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
          <Style TargetType="ListBoxItem">
            <Setter Property="Padding" Value="10"/><Setter Property="Margin" Value="0,0,0,6"/>
            <Setter Property="HorizontalContentAlignment" Value="Stretch"/><Setter Property="Foreground" Value="{DynamicResource Text}"/>
            <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
            <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ListBoxItem">
              <Border Background="{DynamicResource Surface}" BorderBrush="{DynamicResource Line}" BorderThickness="1" CornerRadius="10" Padding="{TemplateBinding Padding}" x:Name="Row"><ContentPresenter/></Border>
              <ControlTemplate.Triggers><Trigger Property="IsSelected" Value="True"><Setter TargetName="Row" Property="Background" Value="{DynamicResource Soft}"/></Trigger><Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="Row" Property="BorderBrush" Value="{DynamicResource Focus}"/><Setter TargetName="Row" Property="BorderThickness" Value="2"/></Trigger></ControlTemplate.Triggers>
            </ControlTemplate></Setter.Value></Setter>
          </Style>
        </ResourceDictionary>
        """;
        var styles = (ResourceDictionary)XamlReader.Parse(xaml);
        Resources.MergedDictionaries.Add(styles);
    }

    private static string Percent(int? value) => value is { } v ? $"~{v}%" : "unavailable";
    private static string Age(double? seconds) => seconds is null ? "unavailable" : seconds < 2 ? "just now" : seconds < 60 ? $"{seconds:0} s ago" : $"{seconds / 60:0} min ago";

    private sealed class CandidateRow
    {
        public ListBoxItem Item { get; }
        public TextBlock Summary { get; } = Text("", 13);
        public TextBlock Time { get; } = Text("", 12);
        public bool CanSelect { get; set; }
        public CandidateRow(string label)
        {
            var panel = new StackPanel();
            panel.Children.Add(Text(label, 15, FontWeights.SemiBold));
            Summary.Margin = new Thickness(0, 5, 0, 3); panel.Children.Add(Summary);
            Time.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); panel.Children.Add(Time);
            Item = new ListBoxItem { Tag = label, Content = panel };
            AutomationProperties.SetName(Item, label);
        }
    }

    private sealed class BatteryView
    {
        public StackPanel Root { get; } = new() { Margin = new Thickness(0, 0, 12, 0) };
        public TextBlock Value { get; } = Text("—", 34, FontWeights.SemiBold);
        private readonly ProgressBar bar = new() { Minimum = 0, Maximum = 100, Height = 5, BorderThickness = new Thickness(0), Margin = new Thickness(0, 14, 0, 0), Focusable = false };
        private readonly TextBlock caption = Text("Unavailable", 12);
        private readonly string label;
        public BatteryView(string label, bool isCase)
        {
            this.label = label;
            var glyph = new System.Windows.Shapes.Path
            {
                Data = Geometry.Parse(isCase ? "M 5,10 Q 5,5 10,5 L 38,5 Q 43,5 43,10 L 43,26 Q 43,31 38,31 L 10,31 Q 5,31 5,26 Z M 5,14 L 43,14"
                    : "M 16,3 Q 7,3 7,12 Q 7,21 16,21 L 18,21 L 18,33 Q 18,36 22,36 Q 25,36 25,33 L 25,13 Q 25,3 16,3 Z"),
                StrokeThickness = 1.7, Height = 36, Width = 48, Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 0, 0, 16), Focusable = false
            };
            glyph.SetResourceReference(System.Windows.Shapes.Shape.StrokeProperty, "Muted"); Root.Children.Add(glyph);
            Root.Children.Add(Text(label, 14, FontWeights.SemiBold));
            Value.TextWrapping = TextWrapping.NoWrap;
            Value.Margin = new Thickness(0, 6, 0, 0); Root.Children.Add(Value);
            caption.SetResourceReference(TextBlock.ForegroundProperty, "Muted"); Root.Children.Add(caption);
            bar.SetResourceReference(Control.BackgroundProperty, "Soft");
            bar.SetResourceReference(Control.ForegroundProperty, "Bar"); Root.Children.Add(bar);
        }
        public void Set(int? percent, bool fresh)
        {
            Value.Text = percent is { } p ? $"~{p}%" : "—";
            Value.SetResourceReference(TextBlock.ForegroundProperty, fresh ? "Text" : "Muted");
            caption.Text = percent is null ? "Unavailable" : fresh ? "Approximate" : "Last estimate";
            bar.Visibility = percent.HasValue ? Visibility.Visible : Visibility.Hidden;
            bar.Value = percent ?? 0;
            bar.SetResourceReference(Control.ForegroundProperty, fresh ? "Bar" : "Muted");
            AutomationProperties.SetName(Value, $"{label}: {(percent.HasValue ? $"approximately {percent} percent, {(fresh ? "fresh" : "historical")}" : "unavailable")}");
            AutomationProperties.SetName(bar, $"{label} approximate battery level");
        }
    }
}
