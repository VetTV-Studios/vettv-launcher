namespace VetTV.Launcher.Core.PlayStation;
/// PS3 games from the user's own disc dumps (<dir>/PS3_GAME/PARAM.SFO) and PSN installs (RPCS3 dev_hdd0/game/<ID>/PARAM.SFO, CATEGORY HG).
/// Encrypted/undumped content is not handled.
public static class Ps3LibraryScanner
{
    public static IReadOnlyList<GameEntry> Scan(IEnumerable<string> dumpRoots, string? rpcs3DevHdd0)
    {
        var games = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in dumpRoots.Where(Directory.Exists))
            foreach (var dir in Directory.EnumerateDirectories(root))
                TryAdd(games, Path.Combine(dir, "PS3_GAME"), requireCategory: null);
        if (rpcs3DevHdd0 is not null && Directory.Exists(Path.Combine(rpcs3DevHdd0, "game")))
            foreach (var dir in Directory.EnumerateDirectories(Path.Combine(rpcs3DevHdd0, "game")))
                TryAdd(games, dir, requireCategory: "HG");
        return games.Values.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
    static void TryAdd(Dictionary<string, GameEntry> games, string gameDir, string? requireCategory)
    {
        var sfo = Path.Combine(gameDir, "PARAM.SFO");
        var eboot = Path.Combine(gameDir, "USRDIR", "EBOOT.BIN");
        if (!File.Exists(sfo) || !File.Exists(eboot)) return;
        try
        {
            var kv = ParamSfo.Parse(File.ReadAllBytes(sfo));
            if (!kv.TryGetValue("TITLE_ID", out var id) || !kv.TryGetValue("TITLE", out var title)) return;
            if (requireCategory != null && kv.GetValueOrDefault("CATEGORY") != requireCategory) return;
            games.TryAdd(id, new GameEntry($"ps3:{id}", title, GameSource.Emulator, gameDir, eboot));
        }
        catch (InvalidDataException) { }
    }
}
