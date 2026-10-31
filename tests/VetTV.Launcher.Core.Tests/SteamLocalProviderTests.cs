using VetTV.Launcher.Core;
using VetTV.Launcher.Core.Providers;
public class SteamLocalProviderTests
{
    const string LibFolders = """
"libraryfolders"
{
	"0"
	{
		"path"		"ROOT"
		"apps" { "620" "123" }
	}
	"1"
	{
		"path"		"LIB2"
	}
}
""";
    static string Acf(string id, string name, string dir) =>
        $"\"AppState\"\n{{\n\t\"appid\"\t\t\"{id}\"\n\t\"name\"\t\t\"{name}\"\n\t\"installdir\"\t\t\"{dir}\"\n}}\n";

    [Fact]
    public void Vdf_parses_nested_and_escapes()
    {
        var n = Vdf.Parse("\"a\" { \"b\" \"C:\\\\Games\" // c\n \"d\" { } }");
        Assert.Equal(@"C:\Games", n["a"]!.Get("b"));
        Assert.NotNull(n["a"]!["d"]);
    }

    [Fact]
    public void ParseAppManifest_builds_entry()
    {
        var g = SteamLocalProvider.ParseAppManifest(Acf("620", "Portal 2", "Portal 2"), "/lib")!;
        Assert.Equal("steam:620", g.Id);
        Assert.Equal("steam://rungameid/620", g.LaunchUri);
        Assert.Equal(Path.Combine("/lib", "steamapps", "common", "Portal 2"), g.InstallPath);
    }

    [Fact]
    public void Malformed_manifest_returns_null()
        => Assert.Null(SteamLocalProvider.ParseAppManifest("\"AppState\" { \"name\" \"x\" }", "/lib"));

    [Fact]
    public async Task Scans_all_library_folders()
    {
        var root = Directory.CreateTempSubdirectory().FullName;
        var lib2 = Directory.CreateTempSubdirectory().FullName;
        Directory.CreateDirectory(Path.Combine(root, "steamapps"));
        Directory.CreateDirectory(Path.Combine(lib2, "steamapps"));
        File.WriteAllText(Path.Combine(root, "steamapps", "libraryfolders.vdf"),
            LibFolders.Replace("ROOT", root.Replace("\\", "\\\\")).Replace("LIB2", lib2.Replace("\\", "\\\\")));
        File.WriteAllText(Path.Combine(root, "steamapps", "appmanifest_620.acf"), Acf("620", "Portal 2", "Portal 2"));
        File.WriteAllText(Path.Combine(lib2, "steamapps", "appmanifest_570.acf"), Acf("570", "Dota 2", "dota 2 beta"));
        File.WriteAllText(Path.Combine(lib2, "steamapps", "appmanifest_1.acf"), "\"AppState\" {");
        var p = new SteamLocalProvider(root);
        Assert.True(p.IsAvailable());
        Assert.Equal(2, p.GetLibraryPaths().Count);
        var games = await p.GetGamesAsync();
        Assert.Equal(new[] { "Dota 2", "Portal 2" }, games.Select(g => g.Title));
        Assert.All(games, g => Assert.Equal(GameSource.Steam, g.Source));
    }

    [Fact]
    public void Missing_steam_is_unavailable()
        => Assert.False(new SteamLocalProvider("/nonexistent").IsAvailable());
}
