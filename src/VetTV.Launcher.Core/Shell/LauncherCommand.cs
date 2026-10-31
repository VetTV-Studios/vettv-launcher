namespace VetTV.Launcher.Core.Shell;
/// Builds the safe process start info for a game's launch URI (no shell-injection via titles).
public static class LauncherCommand
{
    static readonly string[] Allowed = { "steam://", "shell:AppsFolder\\", "com.epicgames.launcher://", "goggalaxy://" };
    public static bool IsAllowed(string uri) => Allowed.Any(p => uri.StartsWith(p, StringComparison.OrdinalIgnoreCase));
}
