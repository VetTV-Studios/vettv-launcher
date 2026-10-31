namespace VetTV.Launcher.Core;
public enum GameSource { Steam, Epic, Gog, Xbox, Emulator }
public sealed record GameEntry(string Id, string Title, GameSource Source, string? InstallPath, string LaunchUri, bool IsInstalled = true);
