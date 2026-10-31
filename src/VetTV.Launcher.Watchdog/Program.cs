using System.Diagnostics;
using System.Runtime.Versioning;
using VetTV.Launcher.Core.Platform;
using VetTV.Launcher.Core.Shell;

// VetTV shell watchdog. HKCU Winlogon\Shell points HERE, not at the UI.
// It runs the launcher, restarts it after a crash, and falls back to explorer.exe
// (restoring the registry) after 3 failures in 2 minutes or if the launcher is missing.
[SupportedOSPlatform("windows")]
static class Program
{
    static readonly string LogDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VetTV", "Launcher");

    static int Main(string[] args)
    {
        Directory.CreateDirectory(LogDir);
        var launcher = Path.Combine(AppContext.BaseDirectory, "VetTV.Launcher.App.exe");
        var policy = new WatchdogPolicy();
        var shell = new ShellRecoveryService(new WindowsRegistryStore());

        // Safe-mode escape: hold Shift during sign-in, or a flag file left by recovery steps.
        if (File.Exists(Path.Combine(LogDir, "SAFE_MODE")) || (GetAsyncKeyState(0x10) & 0x8000) != 0)
            return Fallback(shell, "safe mode requested");

        while (true)
        {
            var started = DateTimeOffset.Now;
            WatchdogDecision d;
            try
            {
                using var p = Process.Start(new ProcessStartInfo(launcher, "--shell") { UseShellExecute = false })
                              ?? throw new InvalidOperationException("no process");
                p.WaitForExit();
                Log($"launcher exited code={p.ExitCode}");
                d = policy.OnExit(p.ExitCode, started, DateTimeOffset.Now);
            }
            catch (Exception ex)
            {
                Log($"launcher failed to start: {ex.GetType().Name}");
                d = policy.OnStartFailed(DateTimeOffset.Now);
                if (d == WatchdogDecision.Restart) Thread.Sleep(2000);
            }
            switch (d)
            {
                case WatchdogDecision.Restart: continue;
                case WatchdogDecision.ExitToDesktop: StartExplorer(); return 0; // shell mode stays on for next sign-in
                default: return Fallback(shell, "too many failures or shell mode disabled");
            }
        }
    }

    static int Fallback(ShellRecoveryService shell, string why)
    {
        Log($"FALLBACK: {why}; restoring explorer shell");
        try { shell.RestoreExplorerShell(); } catch (Exception ex) { Log($"restore failed: {ex.GetType().Name}"); }
        StartExplorer();
        return 1;
    }

    static void StartExplorer()
    {
        var exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
        Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false });
    }

    static void Log(string msg) =>
        File.AppendAllText(Path.Combine(LogDir, "watchdog.log"), $"{DateTimeOffset.Now:O} {msg}{Environment.NewLine}");

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern short GetAsyncKeyState(int vKey);
}
