using VetTV.Launcher.Core.Platform;
namespace VetTV.Launcher.Core.Providers;
/// GOG installs from HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\<id> (written by GOG Galaxy and GOG offline installers).
/// Launch through GalaxyClient (/command=runGame /gameId=<id>) when Galaxy is installed, else the game's exe.
public sealed class GogProvider : ILibraryProvider
{
    public const string GamesKey = @"SOFTWARE\WOW6432Node\GOG.com\Games";
    public const string GalaxyPathsKey = @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient\paths";
    public const string GalaxyClientKey = @"SOFTWARE\WOW6432Node\GOG.com\GalaxyClient";
    readonly IMachineRegistry _reg;
    readonly Func<string, bool> _fileExists;
    public GogProvider(IMachineRegistry reg, Func<string, bool>? fileExists = null) { _reg = reg; _fileExists = fileExists ?? File.Exists; }
    public GameSource Source => GameSource.Gog;
    public bool IsAvailable() => _reg.GetSubKeyNames(GamesKey).Count > 0;

    public string? GalaxyClientExe()
    {
        var dir = _reg.GetValue(GalaxyPathsKey, "client");
        var exe = _reg.GetValue(GalaxyClientKey, "clientExecutable") ?? "GalaxyClient.exe";
        if (string.IsNullOrWhiteSpace(dir)) return null;
        var full = Path.Combine(dir, exe).Replace('/', '\\');
        return _fileExists(full) ? full : null;
    }

    public Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default)
    {
        var galaxy = GalaxyClientExe();
        var list = new List<GameEntry>();
        foreach (var id in _reg.GetSubKeyNames(GamesKey))
        {
            ct.ThrowIfCancellationRequested();
            var k = $@"{GamesKey}\{id}";
            var gameId = _reg.GetValue(k, "gameID") ?? id;
            if (!gameId.All(char.IsDigit)) continue;
            // DLC keys carry a dependsOn pointing at the base game
            if (!string.IsNullOrWhiteSpace(_reg.GetValue(k, "dependsOn"))) continue;
            var title = _reg.GetValue(k, "gameName") ?? _reg.GetValue(k, "GAMENAME");
            if (string.IsNullOrWhiteSpace(title)) continue;
            var path = _reg.GetValue(k, "path");
            var exe = _reg.GetValue(k, "exe");
            string? launchExe; IReadOnlyList<string>? args;
            if (galaxy is not null) { launchExe = galaxy; args = new[] { "/command=runGame", $"/gameId={gameId}" }; }
            else if (!string.IsNullOrWhiteSpace(exe)) { launchExe = exe; args = Array.Empty<string>(); }
            else continue;
            list.Add(new GameEntry($"gog:{gameId}", title, GameSource.Gog, path,
                $"goggalaxy://openGameView/{gameId}", LaunchExe: launchExe, LaunchArgs: args));
        }
        return Task.FromResult<IReadOnlyList<GameEntry>>(list.OrderBy(g => g.Title).ToList());
    }
}
