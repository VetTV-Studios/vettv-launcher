using VetTV.Launcher.Core;
using VetTV.Launcher.Core.Input;
using VetTV.Launcher.Core.Library;
using VetTV.Launcher.Core.Platform;
using VetTV.Launcher.Core.Providers;
using VetTV.Launcher.Core.Security;
using VetTV.Launcher.Core.Shell;
using VetTV.Launcher.Core.Startup;

public class ShellRecoveryTests
{
    const string W = ShellRecoveryService.WinlogonKey;
    [Fact] public void Detects_and_restores_vettv_shell()
    {
        var r = new InMemoryRegistryStore(); r.SetValue(W, "Shell", "\"C:\\VetTV\\VetTV.Launcher.Watchdog.exe\"");
        var s = new ShellRecoveryService(r);
        Assert.Equal(ShellMode.RegistryShell, s.Current);
        s.RestoreExplorerShell();
        Assert.Null(r.GetValue(W, "Shell"));
        Assert.Equal(ShellMode.Normal, s.Current);
    }
    [Fact] public void Restore_puts_back_previous_custom_shell()
    {
        var r = new InMemoryRegistryStore(); r.SetValue(W, "Shell", "\"C:\\VetTV\\VetTV.Launcher.Watchdog.exe\"");
        r.SetValue(ShellRecoveryService.VetTvKey, ShellRecoveryService.PreviousShellValue, "other.exe");
        new ShellRecoveryService(r).RestoreExplorerShell();
        Assert.Equal("other.exe", r.GetValue(W, "Shell"));
        Assert.Null(r.GetValue(ShellRecoveryService.VetTvKey, ShellRecoveryService.PreviousShellValue));
    }
    [Fact] public async Task Free_edition_has_no_shell_setup()
    {
        var f = new VetTV.Launcher.Core.Pro.FreeEdition();
        Assert.Null(f.ShellModeSetup);
        Assert.False((await f.GetLicenseAsync()).HasPro);
    }
}

public class WatchdogPolicyTests
{
    static readonly DateTimeOffset T0 = new(2026, 10, 30, 12, 0, 0, TimeSpan.Zero);
    [Fact] public void Crash_restarts_then_falls_back_after_three_in_window()
    {
        var p = new WatchdogPolicy();
        Assert.Equal(WatchdogDecision.Restart, p.OnExit(-1073741819, T0, T0.AddMinutes(5)));
        Assert.Equal(WatchdogDecision.Restart, p.OnExit(1, T0, T0.AddMinutes(5).AddSeconds(30)));
        Assert.Equal(WatchdogDecision.FallbackToExplorer, p.OnExit(1, T0, T0.AddMinutes(6)));
    }
    [Fact] public void Old_failures_expire()
    {
        var p = new WatchdogPolicy();
        p.OnExit(1, T0, T0.AddMinutes(1)); p.OnExit(1, T0, T0.AddMinutes(2));
        Assert.Equal(WatchdogDecision.Restart, p.OnExit(1, T0, T0.AddMinutes(10)));
    }
    [Fact] public void Clean_exit_goes_to_desktop() =>
        Assert.Equal(WatchdogDecision.ExitToDesktop, new WatchdogPolicy().OnExit(0, T0, T0.AddMinutes(1)));
    [Fact] public void Quick_exit_counts_as_failed_start()
    {
        var p = new WatchdogPolicy();
        Assert.Equal(WatchdogDecision.Restart, p.OnExit(0, T0, T0.AddSeconds(1)));
        Assert.Single(p.Failures);
    }
    [Fact] public void Start_failures_fall_back()
    {
        var p = new WatchdogPolicy();
        p.OnStartFailed(T0); p.OnStartFailed(T0.AddSeconds(2));
        Assert.Equal(WatchdogDecision.FallbackToExplorer, p.OnStartFailed(T0.AddSeconds(4)));
    }
    [Fact] public void Disable_code_falls_back() =>
        Assert.Equal(WatchdogDecision.FallbackToExplorer, new WatchdogPolicy().OnExit(LauncherExitCodes.DisableShellMode, T0, T0.AddHours(1)));
}

public class NavMapperTests
{
    static readonly DateTimeOffset T0 = DateTimeOffset.UnixEpoch;
    [Fact] public void Buttons_are_edge_triggered()
    {
        var m = new NavMapper();
        Assert.Equal(new[] { NavAction.Accept }, m.Update(new(PadButtons.A), T0));
        Assert.Empty(m.Update(new(PadButtons.A), T0.AddMilliseconds(16)));
        Assert.Empty(m.Update(new(PadButtons.None), T0.AddMilliseconds(32)));
        Assert.Equal(new[] { NavAction.Back }, m.Update(new(PadButtons.B), T0.AddMilliseconds(48)));
    }
    [Fact] public void Direction_repeats_after_delay()
    {
        var m = new NavMapper();
        Assert.Equal(new[] { NavAction.Down }, m.Update(new(PadButtons.DPadDown), T0));
        Assert.Empty(m.Update(new(PadButtons.DPadDown), T0.AddMilliseconds(300)));
        Assert.Equal(new[] { NavAction.Down }, m.Update(new(PadButtons.DPadDown), T0.AddMilliseconds(400)));
        Assert.Empty(m.Update(new(PadButtons.DPadDown), T0.AddMilliseconds(450)));
        Assert.Equal(new[] { NavAction.Down }, m.Update(new(PadButtons.DPadDown), T0.AddMilliseconds(520)));
    }
    [Fact] public void Stick_respects_deadzone()
    {
        var m = new NavMapper();
        Assert.Empty(m.Update(new(PadButtons.None, 0.3, 0), T0));
        Assert.Equal(new[] { NavAction.Right }, m.Update(new(PadButtons.None, 0.9, 0), T0.AddMilliseconds(16)));
    }
    [Fact] public void Recovery_chord_fires_once_after_hold()
    {
        var m = new NavMapper(); var chord = PadButtons.View | PadButtons.Menu;
        Assert.Empty(m.Update(new(chord), T0));
        Assert.Empty(m.Update(new(chord), T0.AddSeconds(2)));
        Assert.Equal(new[] { NavAction.RecoveryRequested }, m.Update(new(chord), T0.AddSeconds(3)));
        Assert.Empty(m.Update(new(chord), T0.AddSeconds(4)));
    }
}

public class StartupAndLibraryTests
{
    [Fact] public void Startup_toggle_writes_run_key()
    {
        var r = new InMemoryRegistryStore(); var s = new StartupService(r);
        s.Enable(@"C:\VetTV\VetTV.Launcher.App.exe");
        Assert.True(s.IsEnabled);
        Assert.Equal("\"C:\\VetTV\\VetTV.Launcher.App.exe\" --startup", r.GetValue(StartupService.RunKey, StartupService.ValueName));
        s.Disable(); Assert.False(s.IsEnabled);
    }

    sealed class Fake(GameSource src, Func<IReadOnlyList<GameEntry>> f) : ILibraryProvider
    {
        public GameSource Source => src; public bool IsAvailable() => true;
        public Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default) => Task.FromResult(f());
    }
    [Fact] public async Task Library_merges_sorts_dedupes_and_survives_failures()
    {
        var a = new Fake(GameSource.Steam, () => new[] { new GameEntry("steam:2", "zeta", GameSource.Steam, null, "steam://rungameid/2"), new GameEntry("steam:1", "Alpha", GameSource.Steam, null, "steam://rungameid/1") });
        var dup = new Fake(GameSource.Steam, () => new[] { new GameEntry("STEAM:1", "Alpha dup", GameSource.Steam, null, "x") });
        var bad = new Fake(GameSource.Epic, () => throw new IOException("token=abc"));
        var lib = new LibraryService(new ILibraryProvider[] { a, dup, bad });
        var games = await lib.LoadAsync();
        Assert.Equal(new[] { "Alpha", "zeta" }, games.Select(g => g.Title));
        Assert.Equal(new[] { "Epic: IOException" }, lib.Errors); // no exception message (could hold secrets)
    }

    [Fact] public async Task Xbox_filter_keeps_only_games()
    {
        StorePackageInfo P(string n, bool game, bool fw = false, bool sys = false) => new($"{n}_8wekyb3d8bbwe", n, $"{n}!App", $@"C:\XboxGames\{n}", fw, false, game, sys);
        var x = new XboxProvider(_ => Task.FromResult<IEnumerable<StorePackageInfo>>(new[] { P("Forza", true), P("Calculator", false), P("VCLibs", true, fw: true), P("Sys", true, sys: true) }));
        var g = Assert.Single(await x.GetGamesAsync());
        Assert.Equal("xbox:Forza_8wekyb3d8bbwe", g.Id);
        Assert.Equal(@"shell:AppsFolder\Forza!App", g.LaunchUri);
        Assert.False(new XboxProvider().IsAvailable());
    }

    [Theory]
    [InlineData("steam://rungameid/620", true)]
    [InlineData(@"shell:AppsFolder\Forza!App", true)]
    [InlineData("file:///C:/evil.exe", false)]
    [InlineData("cmd.exe /c", false)]
    public void Launch_allowlist(string uri, bool ok) => Assert.Equal(ok, LauncherCommand.IsAllowed(uri));

    [Theory]
    [InlineData("GET /x?access_token=abc123&y=1", "GET /x?access_token=***&y=1")]
    [InlineData("{\"refresh_token\":\"r-9z\"}", "{\"refresh_token\":\"***\"}")]
    [InlineData("Authorization: Bearer eyJhbGc.def", "Authorization: ***")]
    [InlineData("hello world", "hello world")]
    public void Redactor_strips_tokens(string input, string expected) => Assert.Equal(expected, LogRedactor.Redact(input));
}
