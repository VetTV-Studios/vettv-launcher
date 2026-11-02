using VetTV.Launcher.Core;
using VetTV.Launcher.Core.Library;
using VetTV.Launcher.Core.Platform;
using VetTV.Launcher.Core.Providers;

public class EpicProviderTests
{
    static string Dir => Path.Combine(AppContext.BaseDirectory, "Fixtures", "epic");

    [Fact] public async Task Reads_installed_games_and_skips_dlc_incomplete_and_corrupt()
    {
        var g = Assert.Single(await new EpicProvider(Dir).GetGamesAsync());
        Assert.Equal("epic:Min", g.Id);
        Assert.Equal("Hades", g.Title);
        Assert.Equal(GameSource.Epic, g.Source);
        Assert.Equal(@"C:\Program Files\Epic Games\Hades", g.InstallPath);
        Assert.Equal("com.epicgames.launcher://apps/min-ns%3Acat123%3AMin?action=launch&silent=true", g.LaunchUri);
    }
    [Fact] public async Task Missing_folder_is_soft()
    {
        var p = new EpicProvider(Path.Combine(Dir, "nope"));
        Assert.False(p.IsAvailable());
        Assert.Empty(await p.GetGamesAsync());
    }
    [Fact] public void Launch_uri_escapes_parts() =>
        Assert.Equal("com.epicgames.launcher://apps/a%20b%3Ac%3Ad%26e?action=launch&silent=true", EpicProvider.LaunchUri("a b", "c", "d&e"));
}

public class GogProviderTests
{
    const string K = GogProvider.GamesKey;
    static InMemoryMachineRegistry Reg(bool galaxy)
    {
        var r = new InMemoryMachineRegistry();
        r.Set($@"{K}\1207658924", "gameID", "1207658924"); r.Set($@"{K}\1207658924", "gameName", "Cyber Game");
        r.Set($@"{K}\1207658924", "path", @"C:\GOG Games\Cyber Game"); r.Set($@"{K}\1207658924", "exe", @"C:\GOG Games\Cyber Game\game.exe");
        r.Set($@"{K}\1300000001", "gameID", "1300000001"); r.Set($@"{K}\1300000001", "gameName", "Cyber Game DLC"); r.Set($@"{K}\1300000001", "dependsOn", "1207658924");
        r.Set($@"{K}\1400000002", "gameID", "1400000002"); // no name -> skipped
        if (galaxy) { r.Set(GogProvider.GalaxyPathsKey, "client", @"C:\Program Files (x86)\GOG Galaxy"); r.Set(GogProvider.GalaxyClientKey, "clientExecutable", "GalaxyClient.exe"); }
        return r;
    }
    [Fact] public async Task Uses_galaxy_when_installed()
    {
        var p = new GogProvider(Reg(true), _ => true);
        var g = Assert.Single(await p.GetGamesAsync());
        Assert.Equal("gog:1207658924", g.Id);
        Assert.Equal("Cyber Game", g.Title);
        Assert.Equal(@"C:\Program Files (x86)\GOG Galaxy\GalaxyClient.exe", g.LaunchExe);
        Assert.Equal(new[] { "/command=runGame", "/gameId=1207658924" }, g.LaunchArgs);
    }
    [Fact] public async Task Falls_back_to_game_exe_without_galaxy()
    {
        var g = Assert.Single(await new GogProvider(Reg(false), _ => false).GetGamesAsync());
        Assert.Equal(@"C:\GOG Games\Cyber Game\game.exe", g.LaunchExe);
        Assert.Empty(g.LaunchArgs!);
    }
    [Fact] public async Task Empty_registry_is_soft()
    {
        var p = new GogProvider(new InMemoryMachineRegistry());
        Assert.False(p.IsAvailable());
        Assert.Empty(await p.GetGamesAsync());
    }
}

public class HeroicProviderTests
{
    static string Root => Path.Combine(AppContext.BaseDirectory, "Fixtures", "heroic");

    [Fact] public async Task Reads_legendary_gog_and_nile_installs()
    {
        var games = (await new HeroicProvider(Root).GetGamesAsync()).ToDictionary(g => g.Id);
        Assert.Equal(new[] { "amazon:amzn1.adg.product.abc", "amazon:amzn1.adg.product.def", "epic:Min", "epic:Quail", "gog:1111", "gog:1207658924" },
            games.Keys.OrderBy(k => k, StringComparer.Ordinal));
        Assert.Equal("heroic://launch/legendary/Quail", games["epic:Quail"].LaunchUri);
        Assert.Equal("heroic://launch/gog/1207658924", games["gog:1207658924"].LaunchUri);
        Assert.Equal("Cyber Game", games["gog:1207658924"].Title);          // from store_cache
        Assert.Equal("Untitled Folder", games["gog:1111"].Title);          // folder-name fallback
        Assert.Equal("Star Game", games["amazon:amzn1.adg.product.abc"].Title);
        Assert.Equal("Folder Name Only", games["amazon:amzn1.adg.product.def"].Title);
        Assert.Equal("heroic://launch/nile/amzn1.adg.product.abc", games["amazon:amzn1.adg.product.abc"].LaunchUri);
        Assert.All(games.Values, g => Assert.Equal(GameSource.Heroic, g.Source));
    }
    [Fact] public void Never_reads_credential_files()
    {
        Assert.DoesNotContain(HeroicProvider.ReadFiles, f => f.Contains("user", StringComparison.OrdinalIgnoreCase) || f.Contains("auth", StringComparison.OrdinalIgnoreCase));
    }
    [Fact] public async Task Missing_and_corrupt_files_are_soft()
    {
        var tmp = Directory.CreateTempSubdirectory("heroic").FullName;
        try
        {
            Directory.CreateDirectory(Path.Combine(tmp, "gog_store"));
            File.WriteAllText(Path.Combine(tmp, "gog_store", "installed.json"), "{ broken");
            Assert.Empty(await new HeroicProvider(tmp).GetGamesAsync());
            Assert.Empty(await new HeroicProvider(Path.Combine(tmp, "none")).GetGamesAsync());
        }
        finally { Directory.Delete(tmp, true); }
    }
    [Fact] public async Task Native_launcher_wins_duplicates_regardless_of_order()
    {
        var reg = new InMemoryMachineRegistry();
        reg.Set($@"{GogProvider.GamesKey}\1207658924", "gameName", "Cyber Game");
        reg.Set($@"{GogProvider.GamesKey}\1207658924", "exe", @"C:\g.exe");
        var lib = new LibraryService(new ILibraryProvider[] {
            new HeroicProvider(Root),
            new EpicProvider(Path.Combine(AppContext.BaseDirectory, "Fixtures", "epic")),
            new GogProvider(reg, _ => false) });
        var games = (await lib.LoadAsync()).ToDictionary(g => g.Id);
        Assert.Equal(GameSource.Epic, games["epic:Min"].Source);
        Assert.Equal(GameSource.Gog, games["gog:1207658924"].Source);
        Assert.Equal(GameSource.Heroic, games["epic:Quail"].Source);
        Assert.Equal(6, games.Count);
        Assert.Empty(lib.Errors);
    }
}
