using System.Text.Json;
namespace VetTV.Launcher.Core.Providers;
/// Heroic Games Launcher installs (%APPDATA%\heroic). Reads ONLY these install lists and title caches:
///   legendaryConfig\legendary\installed.json  (Epic via Legendary; object keyed by app name)
///   gog_store\installed.json                  (GOG; { "installed": [ { appName, install_path, ... } ] })
///   nile_config\nile\installed.json           (Amazon via Nile; [ { id, path, ... } ]), titles from nile\library.json
///   store_cache\gog_library.json              (optional GOG titles)
/// Never opens user.json, auth.json, current_user.json or any other credential file.
/// Ids use the store prefix (epic:/gog:/amazon:) so the native Epic/GOG providers win on duplicates.
public sealed class HeroicProvider : ILibraryProvider
{
    readonly string _root;
    public HeroicProvider(string heroicRoot) => _root = heroicRoot;
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "heroic");
    public GameSource Source => GameSource.Heroic;
    public bool IsAvailable() => Directory.Exists(_root);

    public static readonly string[] ReadFiles =
    {
        Path.Combine("legendaryConfig", "legendary", "installed.json"),
        Path.Combine("gog_store", "installed.json"),
        Path.Combine("store_cache", "gog_library.json"),
        Path.Combine("nile_config", "nile", "installed.json"),
        Path.Combine("nile_config", "nile", "library.json"),
    };

    public static string LaunchUri(string runner, string appName) => $"heroic://launch/{runner}/{Uri.EscapeDataString(appName)}";

    JsonDocument? Load(int i) => JsonSoft.Load(Path.Combine(_root, ReadFiles[i]));

    public IEnumerable<GameEntry> Legendary()
    {
        using var d = Load(0);
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Object) yield break;
        foreach (var p in d.RootElement.EnumerateObject())
        {
            var g = p.Value; if (g.ValueKind != JsonValueKind.Object || g.Bool("is_dlc")) continue;
            var app = g.Str("app_name") ?? p.Name;
            yield return new GameEntry($"epic:{app}", g.Str("title") ?? app, GameSource.Heroic, g.Str("install_path"), LaunchUri("legendary", app));
        }
    }

    public IEnumerable<GameEntry> Gog()
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var lib = Load(2))
            if (lib is not null && lib.RootElement.ValueKind == JsonValueKind.Object && lib.RootElement.TryGetProperty("games", out var arr) && arr.ValueKind == JsonValueKind.Array)
                foreach (var g in arr.EnumerateArray())
                    if (g.Str("app_name") is { } a && g.Str("title") is { } t) titles[a] = t;
        using var d = Load(1);
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Object || !d.RootElement.TryGetProperty("installed", out var inst) || inst.ValueKind != JsonValueKind.Array) yield break;
        foreach (var g in inst.EnumerateArray())
        {
            if (g.Bool("is_dlc") || g.Str("appName") is not { } app) continue;
            var path = g.Str("install_path");
            yield return new GameEntry($"gog:{app}", titles.GetValueOrDefault(app) ?? JsonSoft.FolderTitle(path, app), GameSource.Heroic, path, LaunchUri("gog", app));
        }
    }

    public IEnumerable<GameEntry> Nile()
    {
        var titles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        using (var lib = Load(4))
            if (lib is not null && lib.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var e in lib.RootElement.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty("product", out var pr) && pr.Str("id") is { } id && pr.Str("title") is { } t) titles[id] = t;
        using var d = Load(3);
        if (d is null || d.RootElement.ValueKind != JsonValueKind.Array) yield break;
        foreach (var g in d.RootElement.EnumerateArray())
        {
            if (g.Str("id") is not { } id) continue;
            var path = g.Str("path");
            yield return new GameEntry($"amazon:{id}", titles.GetValueOrDefault(id) ?? JsonSoft.FolderTitle(path, id), GameSource.Heroic, path, LaunchUri("nile", id));
        }
    }

    public Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default)
    {
        var all = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        if (IsAvailable())
            foreach (var g in Legendary().Concat(Gog()).Concat(Nile())) { ct.ThrowIfCancellationRequested(); all.TryAdd(g.Id, g); }
        return Task.FromResult<IReadOnlyList<GameEntry>>(all.Values.OrderBy(g => g.Title).ToList());
    }
}
