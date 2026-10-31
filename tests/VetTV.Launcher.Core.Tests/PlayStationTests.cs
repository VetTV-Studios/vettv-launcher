using System.Net;
using System.Text;
using VetTV.Launcher.Core;
using VetTV.Launcher.Core.PlayStation;

public class SystemCnfTests
{
    [Theory]
    [InlineData("BOOT2 = cdrom0:\\SLUS_203.12;1\r\nVER = 1.00\r\nVMODE = NTSC", PsSystem.PS2, "SLUS-20312")]
    [InlineData("BOOT = cdrom:\\SCUS_944.26;1\nTCB = 4\nEVENT = 10", PsSystem.PS1, "SCUS-94426")]
    [InlineData("BOOT=cdrom:\\\\SLES_123.45;1", PsSystem.PS1, "SLES-12345")]
    [InlineData("BOOT = cdrom:PSX.EXE;1", PsSystem.PS1, null)]
    [InlineData("garbage", PsSystem.Unknown, null)]
    public void Parses(string text, PsSystem sys, string? serial)
    {
        var r = SystemCnf.Parse(text);
        Assert.Equal(sys, r.System); Assert.Equal(serial, r.Serial);
    }
    [Theory, InlineData("SLUS_20312", "SLUS-20312"), InlineData("SCES12345", "SCES-12345"), InlineData("MY DISC", null), InlineData(null, null)]
    public void Label_serial(string? label, string? serial) => Assert.Equal(serial, SystemCnf.SerialFromLabel(label));
}

sealed class FakeDisc(string? cnf, string? label, long size = 650L << 20, params string[] files) : IDiscReader
{
    public string DrivePath => "E:\\"; public string? VolumeLabel => label; public long TotalBytes => size;
    public string? ReadTextFile(string p) => p == "SYSTEM.CNF" ? cnf : null;
    public bool FileExists(string p) => (p == "SYSTEM.CNF" && cnf != null) || files.Contains(p);
}

public class DiscIdentifierTests
{
    static GameDb Db()
    {
        var db = new GameDb();
        db.LoadLibretroDat(new StringReader("game (\n\tname \"Crash Bandicoot (USA)\"\n\tserial \"SCUS-94900\"\n\trom ( name \"x.bin\" )\n)\n"), PsSystem.PS1);
        db.LoadPcsx2GameIndex(new StringReader("SLUS-20312:\n  name: \"Final Fantasy X\"\n  region: \"NTSC-U\"\nSLES-50001:\n  name: Other\n"));
        return db;
    }
    [Fact] public void Ps2_from_cnf_with_title() =>
        Assert.Equal(new DiscInfo(PsSystem.PS2, "SLUS-20312", "Final Fantasy X", "FFX", "E:\\"),
            new DiscIdentifier(Db()).Identify(new FakeDisc("BOOT2 = cdrom0:\\SLUS_203.12;1", "FFX", 4L << 30)));
    [Fact] public void Ps1_from_cnf_with_title()
    {
        var d = new DiscIdentifier(Db()).Identify(new FakeDisc("BOOT = cdrom:\\SCUS_949.00;1", "CRASH"));
        Assert.Equal((PsSystem.PS1, "SCUS-94900", "Crash Bandicoot (USA)"), (d.System, d.Serial, d.Title));
    }
    [Fact] public void Falls_back_to_label_and_db_system()
    {
        var d = new DiscIdentifier(Db()).Identify(new FakeDisc(null, "SLUS_20312"));
        Assert.Equal((PsSystem.PS2, "SLUS-20312", "Final Fantasy X"), (d.System, d.Serial, d.Title));
    }
    [Fact] public void Label_unknown_serial_uses_size()
    {
        Assert.Equal(PsSystem.PS2, new DiscIdentifier(new GameDb()).Identify(new FakeDisc(null, "SLUS_99999", 4L << 30)).System);
        Assert.Equal(PsSystem.PS1, new DiscIdentifier(new GameDb()).Identify(new FakeDisc(null, "SLUS_99999")).System);
    }
    [Fact] public void Early_ps1_without_cnf() =>
        Assert.Equal(PsSystem.PS1, new DiscIdentifier(new GameDb()).Identify(new FakeDisc(null, "RIDGE", 650L << 20, "PSX.EXE")).System);
    [Fact] public void Ps3_disc_is_flagged_not_played()
    {
        var d = new DiscIdentifier(new GameDb()).Identify(new FakeDisc(null, "PS3VOLUME", 25L << 30, "PS3_DISC.SFB"));
        Assert.Equal(PsSystem.PS3, d.System);
        Assert.Equal(LaunchProblem.Ps3DiscNotSupported, PlayStationLauncher.ForDisc(d, new("ra", "core", "pcsx2", "rpcs3"), true).Problem);
    }
    [Fact] public void Audio_cd_is_unknown() =>
        Assert.Equal(PsSystem.Unknown, new DiscIdentifier(new GameDb()).Identify(new FakeDisc(null, "Audio CD")).System);
    [Fact] public void Db_normalizes_serials() => Assert.Equal("Final Fantasy X", Db().Lookup("slus_203.12")?.Title);
}

public class BiosTests
{
    [Fact] public void Known_and_unknown_hashes()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var f = Path.Combine(dir, "bios.bin"); File.WriteAllBytes(f, Encoding.ASCII.GetBytes("hello"));
        // md5("hello") = 5d41402abc4b2a76b9719d911017c592
        Assert.False(new BiosValidator().Check(f).IsKnownGood);
        var extra = BiosValidator.ParseList(new StringReader("# list\n5d41402abc4b2a76b9719d911017c592\tPS2\ttest-bios\nbad line\n"));
        var c = new BiosValidator(extra).Check(f);
        Assert.True(c.IsKnownGood); Assert.Equal(PsSystem.PS2, c.Match!.System); Assert.Equal("5d41402abc4b2a76b9719d911017c592", c.Md5);
        Assert.False(new BiosValidator().Check(Path.Combine(dir, "missing.bin")).IsKnownGood);
    }
    [Fact] public void Ps1_list_has_beetle_hashes() =>
        Assert.Contains(BiosValidator.Ps1, b => b.Md5 == "490f666e1afb15b7362b406ed1cea246" && b.Name.StartsWith("scph5501"));
}

public class LaunchTests
{
    static readonly EmulatorPaths P = new(@"C:\RA\retroarch.exe", @"C:\RA\cores\mednafen_psx_hw_libretro.dll", @"C:\PCSX2\pcsx2-qt.exe", @"C:\RPCS3\rpcs3.exe");
    [Fact] public void Ps2_disc_uses_dash_disc()
    {
        var (s, p) = PlayStationLauncher.ForDisc(new(PsSystem.PS2, "SLUS-20312", "FFX", null, "e:\\"), P, false);
        Assert.Equal(LaunchProblem.None, p);
        Assert.Equal(new[] { "-fullscreen", "-batch", "-disc", "E:" }, s!.Args);
    }
    [Fact] public void Ps1_disc_and_image_fallback()
    {
        var d = new DiscInfo(PsSystem.PS1, "SCUS-94900", "Crash", null, "E:\\");
        Assert.Equal(new[] { "-f", "-L", P.BeetleCore!, "cdrom://e:/drive1.cue" }, PlayStationLauncher.ForDisc(d, P, true).Spec!.Args);
        Assert.Equal(@"D:\img\Crash.cue", PlayStationLauncher.ForDisc(d, P, true, ps1UseImage: true, ps1ImageCue: @"D:\img\Crash.cue").Spec!.Args[^1]);
        Assert.Equal(LaunchProblem.BiosMissing, PlayStationLauncher.ForDisc(d, P, false).Problem);
        Assert.Equal(LaunchProblem.EmulatorMissing, PlayStationLauncher.ForDisc(d, P with { RetroArchExe = null }, true).Problem);
    }
    [Fact] public void Rpcs3_eboot_and_titleid()
    {
        Assert.Equal(new[] { "--no-gui", "--fullscreen", @"D:\PS3\X\PS3_GAME\USRDIR\EBOOT.BIN" }, EmulatorLaunch.Rpcs3("rpcs3", @"D:\PS3\X\PS3_GAME\USRDIR\EBOOT.BIN").Args);
        Assert.Equal("%RPCS3_GAMEID%:BLUS30443", EmulatorLaunch.Rpcs3("rpcs3", "BLUS30443").Args[^1]);
    }
    [Fact] public void Pcsx2_image_uses_double_dash() => Assert.Equal(new[] { "-fullscreen", "-batch", "--", "-odd name.iso" }, EmulatorLaunch.Pcsx2Image("p", "-odd name.iso").Args);
    [Fact] public void Bad_drive_rejected() => Assert.Throws<ArgumentException>(() => EmulatorLaunch.DriveLetter("\\\\server"));
}

sealed class FakeCd(IReadOnlyList<CdTrack> toc, int leadOut, int failAt = -1) : IRawCdReader
{
    public IReadOnlyList<CdTrack> ReadToc(out int l) { l = leadOut; return toc; }
    public int ReadSectors(int lba, int count, Span<byte> buf)
    {
        if (failAt >= lba && failAt < lba + count) return 0;
        for (int i = 0; i < count; i++) buf.Slice(i * 2352, 2352).Fill((byte)((lba + i) % 251));
        return count;
    }
}
public class DiscImageCopyTests
{
    [Fact] public void Msf() { Assert.Equal("00:00:00", DiscImageCopy.Msf(0)); Assert.Equal("01:02:03", DiscImageCopy.Msf(62 * 75 + 3)); }
    [Fact] public void Cue_for_data_plus_audio()
    {
        var cue = DiscImageCopy.BuildCue("Game.bin", new[] { new CdTrack(1, 0, false), new CdTrack(2, 1000, true) });
        Assert.Equal("FILE \"Game.bin\" BINARY\n  TRACK 01 MODE2/2352\n    INDEX 01 00:00:00\n  TRACK 02 AUDIO\n    INDEX 00 00:11:25\n    INDEX 01 00:13:25\n", cue);
    }
    [Fact] public async Task Copies_all_sectors_and_writes_cue()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        var cue = await DiscImageCopy.CopyAsync(new FakeCd(new[] { new CdTrack(1, 0, false) }, 40), dir, "Crash: Bandicoot?");
        var bin = Path.ChangeExtension(cue, ".bin");
        Assert.Equal(40 * 2352, new FileInfo(bin).Length);
        Assert.Equal(39 % 251, File.ReadAllBytes(bin)[39 * 2352]);
        Assert.Contains("MODE2/2352", File.ReadAllText(cue));
    }
    [Fact] public async Task Read_error_leaves_no_bin()
    {
        var dir = Directory.CreateTempSubdirectory().FullName;
        await Assert.ThrowsAsync<IOException>(() => DiscImageCopy.CopyAsync(new FakeCd(new[] { new CdTrack(1, 0, false) }, 40, failAt: 20), dir, "x"));
        Assert.False(File.Exists(Path.Combine(dir, "x.bin")));
    }
}

public class Ps3Tests
{
    internal static byte[] Sfo(params (string k, string v)[] kv)
    {
        var keys = new MemoryStream(); var data = new MemoryStream(); var idx = new List<(int ko, int len, int doff)>();
        foreach (var (k, v) in kv)
        {
            int ko = (int)keys.Length; keys.Write(Encoding.UTF8.GetBytes(k + "\0"));
            var b = Encoding.UTF8.GetBytes(v + "\0"); int doff = (int)data.Length; data.Write(b);
            idx.Add((ko, b.Length, doff));
        }
        while (keys.Length % 4 != 0) keys.WriteByte(0);
        int keyTable = 20 + 16 * kv.Length, dataTable = keyTable + (int)keys.Length;
        var o = new MemoryStream(); var w = new BinaryWriter(o);
        w.Write(new byte[] { 0, (byte)'P', (byte)'S', (byte)'F' }); w.Write(0x0101); w.Write(keyTable); w.Write(dataTable); w.Write(kv.Length);
        foreach (var (ko, len, doff) in idx) { w.Write((ushort)ko); w.Write((ushort)0x0204); w.Write(len); w.Write(len); w.Write(doff); }
        w.Write(keys.ToArray()); w.Write(data.ToArray()); return o.ToArray();
    }
    [Fact] public void Parses_sfo()
    {
        var kv = ParamSfo.Parse(Sfo(("TITLE", "Demon's Souls"), ("TITLE_ID", "BLUS30443"), ("CATEGORY", "DG")));
        Assert.Equal("Demon's Souls", kv["TITLE"]); Assert.Equal("BLUS30443", kv["TITLE_ID"]);
        Assert.Throws<InvalidDataException>(() => ParamSfo.Parse(new byte[32]));
    }
    static void Game(string dir, byte[] sfo)
    {
        Directory.CreateDirectory(Path.Combine(dir, "USRDIR"));
        File.WriteAllBytes(Path.Combine(dir, "PARAM.SFO"), sfo); File.WriteAllBytes(Path.Combine(dir, "USRDIR", "EBOOT.BIN"), new byte[4]);
    }
    [Fact] public void Scans_dumps_and_psn_installs()
    {
        var dumps = Directory.CreateTempSubdirectory().FullName; var hdd = Directory.CreateTempSubdirectory().FullName;
        Game(Path.Combine(dumps, "Demons Souls", "PS3_GAME"), Sfo(("TITLE", "Demon's Souls"), ("TITLE_ID", "BLUS30443"), ("CATEGORY", "DG")));
        Directory.CreateDirectory(Path.Combine(dumps, "not a game"));
        Game(Path.Combine(hdd, "game", "NPUB30695"), Sfo(("TITLE", "Journey"), ("TITLE_ID", "NPUB30695"), ("CATEGORY", "HG")));
        Game(Path.Combine(hdd, "game", "BLUS30443"), Sfo(("TITLE", "Patch"), ("TITLE_ID", "BLUS30443"), ("CATEGORY", "GD"))); // update, not a game
        var g = Ps3LibraryScanner.Scan(new[] { dumps, "/nope" }, hdd);
        Assert.Equal(new[] { "Demon's Souls", "Journey" }, g.Select(x => x.Title));
        Assert.EndsWith("EBOOT.BIN", g[0].LaunchUri);
    }
}

public class EmulatorManagerTests
{
    const string Pcsx2Json = """
    {"tag_name":"v2.2.0","assets":[
      {"name":"pcsx2-v2.2.0-linux-appimage-x64-Qt.AppImage","browser_download_url":"https://github.com/PCSX2/pcsx2/releases/download/v2.2.0/a.AppImage","size":1},
      {"name":"pcsx2-v2.2.0-windows-x64-Qt.7z","browser_download_url":"https://github.com/PCSX2/pcsx2/releases/download/v2.2.0/pcsx2-v2.2.0-windows-x64-Qt.7z","size":5,"digest":"sha256:ABCD"}]}
    """;
    [Fact] public void Picks_windows_asset()
    {
        var src = EmulatorSources.Official[EmulatorKind.Pcsx2];
        var r = EmulatorSources.ParseGitHubRelease(EmulatorKind.Pcsx2, Pcsx2Json, src.AssetPattern)!;
        Assert.Equal(("2.2.0", "pcsx2-v2.2.0-windows-x64-Qt.7z", "ABCD"), (r.Version, r.AssetName, r.Sha256));
    }
    [Fact] public void Rejects_unofficial_asset_urls()
    {
        var json = Pcsx2Json.Replace("https://github.com/PCSX2/pcsx2/releases/download/v2.2.0/pcsx2-v2.2.0-windows-x64-Qt.7z", "https://evil.example/p.7z");
        Assert.Null(EmulatorSources.ParseGitHubRelease(EmulatorKind.Pcsx2, json, EmulatorSources.Official[EmulatorKind.Pcsx2].AssetPattern));
    }
    [Theory]
    [InlineData("https://github.com/PCSX2/pcsx2/releases/download/x/y.7z", true)]
    [InlineData("https://github.com/someone/pcsx2/releases/download/x/y.7z", false)]
    [InlineData("http://buildbot.libretro.com/x", false)]
    [InlineData("https://buildbot.libretro.com/stable/1.19.1/windows/x86_64/RetroArch.7z", true)]
    [InlineData("https://buildbot.libretro.com.evil.io/x", false)]
    public void Official_url_allowlist(string url, bool ok) => Assert.Equal(ok, EmulatorSources.IsOfficialUrl(url));
    [Fact] public void RetroArch_maps_to_buildbot() =>
        Assert.Equal("https://buildbot.libretro.com/stable/1.19.1/windows/x86_64/RetroArch.7z",
            EmulatorSources.ParseGitHubRelease(EmulatorKind.RetroArch, """{"tag_name":"v1.19.1","assets":[]}""", "")!.DownloadUrl);
    [Theory, InlineData("1.7.0", "v2.2.0", true), InlineData("2.2.0", "v2.2.0", false), InlineData("", "0.0.31-16000", true)]
    public void Version_compare(string cur, string cand, bool newer) => Assert.Equal(newer, EmulatorSources.IsNewer(cur, cand));

    sealed class Handler(byte[] body) : HttpMessageHandler
    {
        public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        { Calls++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body), RequestMessage = r }); }
    }
    static EmulatorRelease Rel(string? sha) => new(EmulatorKind.Pcsx2, "2.2.0", "https://github.com/PCSX2/pcsx2/releases/download/v2.2.0/p.7z", "p.7z", 5, sha);
    [Fact] public async Task Download_requires_consent_for_that_release()
    {
        var h = new Handler(Encoding.ASCII.GetBytes("hello")); var inst = new EmulatorInstaller(new HttpClient(h));
        var dir = Directory.CreateTempSubdirectory().FullName;
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => inst.DownloadAsync(null, Rel(null), dir));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => inst.DownloadAsync(new UserConsent(Rel("x"), DateTimeOffset.Now), Rel(null), dir));
        Assert.Equal(0, h.Calls);
        var sha = "2CF24DBA5FB0A30E26E83B2AC5B9E29E1B161E5C1FA7425E73043362938B9824"; // sha256("hello")
        var path = await inst.DownloadAsync(new UserConsent(Rel(sha), DateTimeOffset.Now), Rel(sha), dir);
        Assert.Equal("hello", File.ReadAllText(path));
    }
    [Fact] public async Task Checksum_mismatch_deletes_file()
    {
        var inst = new EmulatorInstaller(new HttpClient(new Handler(new byte[] { 1 })));
        var dir = Directory.CreateTempSubdirectory().FullName; var r = Rel("00");
        await Assert.ThrowsAsync<InvalidDataException>(() => inst.DownloadAsync(new UserConsent(r, DateTimeOffset.Now), r, dir));
        Assert.False(File.Exists(Path.Combine(dir, "p.7z")));
    }
}
