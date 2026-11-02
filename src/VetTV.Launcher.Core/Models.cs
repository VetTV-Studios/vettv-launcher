namespace VetTV.Launcher.Core;
public enum GameSource { Steam, Epic, Gog, Heroic, Emulator }
/// LaunchUri is the protocol launch; LaunchExe/LaunchArgs (when set) is a direct process start that
/// the App prefers if the exe exists (used for GOG Galaxy / plain GOG installs).
public sealed record GameEntry(string Id, string Title, GameSource Source, string? InstallPath, string LaunchUri,
    bool IsInstalled = true, string? LaunchExe = null, IReadOnlyList<string>? LaunchArgs = null);
