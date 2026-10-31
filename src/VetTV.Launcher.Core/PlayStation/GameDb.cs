namespace VetTV.Launcher.Core.PlayStation;
public sealed record GameDbEntry(string Serial, string Title, PsSystem System);
/// Serial => title. Sources (never committed to our repo):
///  - PS1: libretro-database DAT (CC BY-SA 4.0), downloaded with attribution into the user's data folder.
///  - PS2: the user's installed PCSX2 resources/GameIndex.yaml, read at runtime.
public sealed class GameDb
{
    readonly Dictionary<string, GameDbEntry> _map = new(StringComparer.OrdinalIgnoreCase);
    public int Count => _map.Count;
    public GameDbEntry? Lookup(string serial) => _map.TryGetValue(Normalize(serial), out var e) ? e : null;
    public void Add(GameDbEntry e) => _map.TryAdd(Normalize(e.Serial), e with { Serial = Normalize(e.Serial) });
    static string Normalize(string s) => s.Trim().ToUpperInvariant().Replace('_', '-').Replace(".", "");

    /// libretro clrmamepro-style DAT: game ( name "Title (USA)" serial "SCUS-94426" ... )
    public void LoadLibretroDat(TextReader r, PsSystem system)
    {
        string? name = null, line;
        while ((line = r.ReadLine()) != null)
        {
            var t = line.Trim();
            if (t.StartsWith("game (", StringComparison.Ordinal) || t == "game (") name = null;
            else if (t.StartsWith("name \"")) name ??= Quoted(t);
            else if (t.StartsWith("serial \"") && name != null)
                foreach (var s in Quoted(t).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    Add(new GameDbEntry(s, name, system));
        }
    }
    /// PCSX2 GameIndex.yaml: "SLUS-20312:" at column 0, then "  name: ..." (minimal reader, no YAML lib).
    public void LoadPcsx2GameIndex(TextReader r)
    {
        string? serial = null, line;
        while ((line = r.ReadLine()) != null)
        {
            if (line.Length > 0 && !char.IsWhiteSpace(line[0]) && line.TrimEnd().EndsWith(':') && !line.StartsWith('#'))
                serial = line.TrimEnd()[..^1].Trim();
            else if (serial != null && line.StartsWith("  name:"))
            {
                var v = line["  name:".Length..].Trim().Trim('"', '\'');
                Add(new GameDbEntry(serial, v, PsSystem.PS2)); serial = null;
            }
        }
    }
    static string Quoted(string t) { int a = t.IndexOf('"'), b = t.LastIndexOf('"'); return b > a ? t[(a + 1)..b] : ""; }
}
