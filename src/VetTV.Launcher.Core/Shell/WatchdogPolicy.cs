namespace VetTV.Launcher.Core.Shell;
public enum WatchdogDecision { Restart, ExitToDesktop, FallbackToExplorer }
/// Launcher exit codes understood by the watchdog.
public static class LauncherExitCodes
{
    public const int ExitToDesktop = 0;      // user chose "Exit to Desktop" / recovery hotkey
    public const int DisableShellMode = 10;  // user turned shell mode off: restore registry + explorer
}
/// Decides what to do each time the launcher process ends.
/// A "failure" = non-zero/unknown exit code OR exit within MinHealthyRun of starting (failed start).
/// MaxFailures failures inside Window => give up, start explorer.exe and restore the registry shell.
public sealed class WatchdogPolicy
{
    public int MaxFailures { get; init; } = 3;
    public TimeSpan Window { get; init; } = TimeSpan.FromMinutes(2);
    public TimeSpan MinHealthyRun { get; init; } = TimeSpan.FromSeconds(10);
    readonly List<DateTimeOffset> _failures = new();
    public IReadOnlyList<DateTimeOffset> Failures => _failures;

    public WatchdogDecision OnExit(int exitCode, DateTimeOffset startedAt, DateTimeOffset exitedAt)
    {
        if (exitCode == LauncherExitCodes.DisableShellMode) return WatchdogDecision.FallbackToExplorer;
        bool quick = exitedAt - startedAt < MinHealthyRun;
        if (exitCode == LauncherExitCodes.ExitToDesktop && !quick) return WatchdogDecision.ExitToDesktop;
        _failures.Add(exitedAt);
        _failures.RemoveAll(t => exitedAt - t > Window);
        return _failures.Count >= MaxFailures ? WatchdogDecision.FallbackToExplorer : WatchdogDecision.Restart;
    }

    /// Failure to even start the process (missing exe, access denied).
    public WatchdogDecision OnStartFailed(DateTimeOffset at) => OnExit(-1, at, at);
}
