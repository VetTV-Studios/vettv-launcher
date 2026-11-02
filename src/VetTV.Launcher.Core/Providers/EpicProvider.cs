using System.Text.Json;
namespace VetTV.Launcher.Core.Providers;
/// Epic Games Launcher installs from %ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item (JSON).
/// Manifests hold no credentials. Corrupt/partial manifests are skipped.
public sealed class EpicProvider : ILibraryProvider
{
    readonly string _manifestDir;
    public EpicProvider(string manifestDir) => _manifestDir = manifestDir;
    public static string DefaultManifestDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests");
    public GameSource Source => GameSource.Epic;
    public bool IsAvailable() => Directory.Exists(_manifestDir);

    public static string LaunchUri(string ns, string catalogItemId, string appName) =>
        $"com.epicgames.launcher://apps/{Uri.EscapeDataString(ns)}%3A{Uri.EscapeDataString(catalogItemId)}%3A{Uri.EscapeDataString(appName)}?action=launch&silent=true";

    public static GameEntry? ParseManifest(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var r = doc.RootElement;
            var app = r.Str("AppName"); var title = r.Str("DisplayName") ?? app;
            var ns = r.Str("CatalogNamespace") ?? r.Str("MainGameCatalogNamespace");
            var cat = r.Str("CatalogItemId") ?? r.Str("MainGameCatalogItemId");
            if (app is null || title is null || ns is null || cat is null) return null;
            if (r.Bool("bIsIncompleteInstall")) return null;
            // DLC/add-ons point at a different main game app
            var main = r.Str("MainGameAppName");
            if (main is not null && !string.Equals(main, app, StringComparison.OrdinalIgnoreCase)) return null;
            if (r.TryGetProperty("AppCategories", out var cats) && cats.ValueKind == JsonValueKind.Array &&
                cats.EnumerateArray().Any(c => c.GetString() == "addons")) return null;
            return new GameEntry($"epic:{app}", title, GameSource.Epic, r.Str("InstallLocation"), LaunchUri(ns, cat, app));
        }
        catch (JsonException) { return null; }
    }

    public Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default)
    {
        var games = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        if (IsAvailable())
            foreach (var f in Directory.EnumerateFiles(_manifestDir, "*.item"))
            {
                ct.ThrowIfCancellationRequested();
                string text; try { text = File.ReadAllText(f); } catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; }
                if (ParseManifest(text) is { } g) games.TryAdd(g.Id, g);
            }
        return Task.FromResult<IReadOnlyList<GameEntry>>(games.Values.OrderBy(g => g.Title).ToList());
    }
}
