namespace VetTV.Launcher.Core.Providers;
/// Installed Steam games from libraryfolders.vdf + appmanifest_*.acf. Owned-but-not-installed
/// games (Steam Web API GetOwnedGames) come later; the user's API key must live in user settings, never in code.
public sealed class SteamLocalProvider : ILibraryProvider
{
    private readonly string _steamRoot;
    public SteamLocalProvider(string steamRoot) => _steamRoot = steamRoot;
    public GameSource Source => GameSource.Steam;
    public bool IsAvailable() => File.Exists(LibraryFoldersPath);
    string LibraryFoldersPath => Path.Combine(_steamRoot, "steamapps", "libraryfolders.vdf");

    public IReadOnlyList<string> GetLibraryPaths()
    {
        var paths = new List<string> { _steamRoot };
        if (!IsAvailable()) return paths;
        var root = Vdf.Parse(File.ReadAllText(LibraryFoldersPath));
        var lf = root["libraryfolders"] ?? root["LibraryFolders"];
        if (lf is null) return paths;
        foreach (var (key, node) in lf.Children)
        {
            var p = node.Value is not null ? (int.TryParse(key, out _) ? node.Value : null) : node.Get("path");
            if (!string.IsNullOrWhiteSpace(p) && !paths.Contains(p, StringComparer.OrdinalIgnoreCase)) paths.Add(p);
        }
        return paths;
    }

    public static GameEntry? ParseAppManifest(string text, string libraryPath)
    {
        var st = Vdf.Parse(text)["AppState"];
        var id = st?.Get("appid"); var name = st?.Get("name");
        if (id is null || name is null) return null;
        var dir = st!.Get("installdir");
        var full = dir is null ? null : Path.Combine(libraryPath, "steamapps", "common", dir);
        return new GameEntry($"steam:{id}", name, GameSource.Steam, full, $"steam://rungameid/{id}");
    }

    public Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default)
    {
        var games = new Dictionary<string, GameEntry>();
        foreach (var lib in GetLibraryPaths())
        {
            var apps = Path.Combine(lib, "steamapps");
            if (!Directory.Exists(apps)) continue;
            foreach (var f in Directory.EnumerateFiles(apps, "appmanifest_*.acf"))
            {
                ct.ThrowIfCancellationRequested();
                try { var g = ParseAppManifest(File.ReadAllText(f), lib); if (g != null && g.Title != "Steamworks Common Redistributables") games.TryAdd(g.Id, g); }
                catch (FormatException) { /* skip corrupt manifest */ }
            }
        }
        return Task.FromResult<IReadOnlyList<GameEntry>>(games.Values.OrderBy(g => g.Title).ToList());
    }
}
