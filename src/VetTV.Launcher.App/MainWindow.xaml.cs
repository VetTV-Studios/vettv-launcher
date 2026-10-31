using System.Diagnostics;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using VetTV.Launcher.App.Services;
using VetTV.Launcher.Core;
using VetTV.Launcher.Core.Input;
using VetTV.Launcher.Core.Library;
using VetTV.Launcher.Core.Platform;
using VetTV.Launcher.Core.Providers;
using VetTV.Launcher.Core.Pro;
using VetTV.Launcher.Core.Shell;
using VetTV.Launcher.Core.Startup;
namespace VetTV.Launcher.App;

public sealed partial class MainWindow : Window
{
    readonly WindowsRegistryStore _reg = new();
    readonly ShellRecoveryService _shell;
    readonly IProEdition _pro;
    ProLicense _license = ProLicense.Free;
    readonly StartupService _startup;
    readonly WinRtGamepadInput _pad;
    readonly RecoveryHotkey _hotkey;
    bool _loadingToggles;

    public MainWindow()
    {
        InitializeComponent();
        AppWindow.SetPresenter(AppWindowPresenterKind.FullScreen);
        _shell = new ShellRecoveryService(_reg);
_pro = EditionFactory.Create(WinRT.Interop.WindowNative.GetWindowHandle(this), _reg);
        _startup = new StartupService(_reg);
        _loadingToggles = true;
        StartupToggle.IsOn = _startup.IsEnabled;
        ShellToggle.IsOn = _shell.Current == ShellMode.RegistryShell;
        _loadingToggles = false;
        _hotkey = new RecoveryHotkey(this, ExitToDesktop);
        _pad = new WinRtGamepadInput(DispatcherQueue);
        _pad.Action += (_, a) => OnNav(a);
        _pad.Start();
        Closed += (_, _) => { _pad.Stop(); _hotkey.Dispose(); };
        _ = LoadAsync();
        _ = RefreshLicenseAsync();
    }

    async Task RefreshLicenseAsync()
    {
        _license = await _pro.GetLicenseAsync();
        bool shellOn = _shell.Current == ShellMode.RegistryShell;
        // Turning shell mode OFF is always allowed (open recovery); turning it ON needs Pro.
        ShellToggle.IsEnabled = shellOn || (_license.HasPro && _pro.ShellModeSetup != null);
        ShellToggle.Visibility = (_pro.ShellModeSetup != null || shellOn) ? Visibility.Visible : Visibility.Collapsed;
        ProButton.Visibility = _pro.ShellModeSetup != null && !_license.HasPro ? Visibility.Visible : Visibility.Collapsed;
        if (_license.Kind == LicenseKind.Trial) Status.Text += $" · Pro trial until {_license.TrialEndsAt:d}";
    }

    async void ProButton_Click(object sender, RoutedEventArgs e) { await _pro.PurchaseAsync(); await RefreshLicenseAsync(); }

    async Task LoadAsync()
    {
        Status.Text = "Loading library…";
        var steamRoot = _reg.GetValue(@"Software\Valve\Steam", "SteamPath");
        var providers = new List<ILibraryProvider> { new XboxProvider(StoreXboxEnumerator.EnumerateAsync) };
        if (steamRoot != null) providers.Insert(0, new SteamLocalProvider(steamRoot.Replace('/', '\\')));
        var lib = new LibraryService(providers);
        var games = await lib.LoadAsync();
        Library.ItemsSource = games;
        Status.Text = $"{games.Count} games" + (lib.Errors.Count > 0 ? $" · {lib.Errors.Count} source error(s)" : "");
        if (games.Count > 0) { Library.SelectedIndex = 0; (Library.ContainerFromIndex(0) as Control)?.Focus(FocusState.Programmatic); }
    }

    void OnNav(NavAction a)
    {
        switch (a)
        {
            case NavAction.Up: FocusManager.TryMoveFocus(FocusNavigationDirection.Up); break;
            case NavAction.Down: FocusManager.TryMoveFocus(FocusNavigationDirection.Down); break;
            case NavAction.Left: FocusManager.TryMoveFocus(FocusNavigationDirection.Left); break;
            case NavAction.Right: FocusManager.TryMoveFocus(FocusNavigationDirection.Right); break;
            case NavAction.Accept:
                if (FocusManager.GetFocusedElement(Root.XamlRoot) is GridViewItem { Content: GameEntry g }) Launch(g);
                else if (FocusManager.GetFocusedElement(Root.XamlRoot) is Button b) ExitButton_Click(b, new RoutedEventArgs());
                else if (FocusManager.GetFocusedElement(Root.XamlRoot) is ToggleSwitch t) t.IsOn = !t.IsOn;
                break;
            case NavAction.Menu: ExitButton.Focus(FocusState.Keyboard); break;
            case NavAction.RecoveryRequested: ExitToDesktop(); break;
        }
    }

    void Library_ItemClick(object sender, ItemClickEventArgs e) { if (e.ClickedItem is GameEntry g) Launch(g); }

    async void Launch(GameEntry g)
    {
        if (!LauncherCommand.IsAllowed(g.LaunchUri)) { Status.Text = "Blocked unknown launch type"; return; }
        Status.Text = $"Launching {g.Title}…";
        if (g.LaunchUri.StartsWith("shell:AppsFolder", StringComparison.OrdinalIgnoreCase))
            Process.Start(new ProcessStartInfo("explorer.exe", g.LaunchUri) { UseShellExecute = true });
        else
            await Windows.System.Launcher.LaunchUriAsync(new Uri(g.LaunchUri));
    }

    void StartupToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loadingToggles) return;
        if (StartupToggle.IsOn) _startup.Enable(Environment.ProcessPath!); else _startup.Disable();
    }

    void ShellToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_loadingToggles) return;
        if (ShellToggle.IsOn)
        {
            var wd = Path.Combine(AppContext.BaseDirectory, "VetTV.Launcher.Watchdog.exe");
            if (!File.Exists(wd)) { Status.Text = "Watchdog missing: shell mode not enabled"; _loadingToggles = true; ShellToggle.IsOn = false; _loadingToggles = false; return; }
            if (_pro.ShellModeSetup is null || !_license.HasPro) { Status.Text = "Shell mode needs VetTV Launcher Pro"; _loadingToggles = true; ShellToggle.IsOn = false; _loadingToggles = false; return; }
            _pro.ShellModeSetup.Enable(wd, _license);
            Status.Text = "Shell mode on from next sign-in. Recovery: Ctrl+Alt+Shift+F12 or hold View+Menu 3 s.";
        }
        else { _shell.RestoreExplorerShell(); Status.Text = "Shell mode off (Explorer restored at next sign-in)"; }
    }

    void ExitButton_Click(object sender, RoutedEventArgs e) => ExitToDesktop();

    /// In shell mode the watchdog starts explorer.exe on exit code 0; otherwise just make sure Explorer is running.
    void ExitToDesktop()
    {
        if (!App.IsShellSession && Process.GetProcessesByName("explorer").Length == 0)
            Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true });
        Application.Current.Exit();
        Environment.Exit(LauncherExitCodes.ExitToDesktop);
    }
}
