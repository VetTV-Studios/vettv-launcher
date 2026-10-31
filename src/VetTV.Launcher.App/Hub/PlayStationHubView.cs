using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using VetTV.Launcher.Core;
using VetTV.Launcher.Core.PlayStation;
using Windows.Storage.Pickers;
namespace VetTV.Launcher.App.Hub;
/// PlayStation hub (Community build only). Built in code to keep XAML minimal; all controls are XY-focusable.
public sealed class PlayStationHubView : UserControl, IDisposable
{
    static string DataDir => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VetTV", "PlayStation");
    readonly Window _window; readonly OpticalDriveWatcher _watcher; readonly GameDb _db = new();
    readonly TextBlock _disc = Text(28), _status = Text(16), _bios = Text(16), _emus = Text(16);
    readonly Button _play = Btn("Play disc"), _copy = Btn("Copy PS1 disc to image, then play"), _biosBtn = Btn("Choose PS1 BIOS…"),
        _ps3Folder = Btn("Add PS3 dump folder…"), _fw = Btn("PS3 firmware (Sony)…");
    readonly StackPanel _emuButtons = new() { Orientation = Orientation.Horizontal, Spacing = 12 };
    readonly GridView _ps3 = new() { IsItemClickEnabled = true, SelectionMode = ListViewSelectionMode.Single };
    DiscInfo? _current; bool _ps1BiosOk;
    readonly HttpClient _http = new() { DefaultRequestHeaders = { { "User-Agent", "VetTV-Launcher" } } };

    public PlayStationHubView(Window w)
    {
        _window = w;
        Directory.CreateDirectory(DataDir);
        var root = new StackPanel { Spacing = 14, Padding = new Thickness(8) };
        root.Children.Add(Text(20, "Insert a PS1 or PS2 disc. PS3: your own dumps or PSN installs only."));
        root.Children.Add(_disc); root.Children.Add(Row(_play, _copy)); root.Children.Add(_status);
        root.Children.Add(Text(22, "BIOS & firmware")); root.Children.Add(_bios); root.Children.Add(Row(_biosBtn, _fw));
        root.Children.Add(Text(22, "Emulators (official sources, installed only when you say so)")); root.Children.Add(_emus); root.Children.Add(_emuButtons);
        root.Children.Add(Text(22, "PS3 library")); root.Children.Add(_ps3Folder); root.Children.Add(_ps3);
        _ps3.ItemTemplate = (DataTemplate)Microsoft.UI.Xaml.Markup.XamlReader.Load(
            "<DataTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Width='220' Height='100' Background='#1E1E2E' CornerRadius='8' Padding='12'><TextBlock Text='{Binding Title}' Foreground='White' TextWrapping='Wrap'/></Border></DataTemplate>");
        Content = new ScrollViewer { Content = root };

        _play.Click += (_, _) => PlayDisc(useImage: false);
        _copy.Click += async (_, _) => await CopyAndPlayAsync();
        _biosBtn.Click += async (_, _) => await PickBiosAsync();
        _fw.Click += async (_, _) => await Windows.System.Launcher.LaunchUriAsync(new Uri("https://www.playstation.com/en-us/support/hardware/ps3/system-software/"));
        _ps3Folder.Click += async (_, _) => await AddPs3FolderAsync();
        _ps3.ItemClick += (_, e) => { if (e.ClickedItem is GameEntry g) LaunchPs3(g); };

        _watcher = new OpticalDriveWatcher(w);
        _watcher.DiscInserted += d => DispatcherQueue.TryEnqueue(() => OnDisc(d));
        _watcher.DiscRemoved += d => DispatcherQueue.TryEnqueue(() => { if (_current?.DrivePath == d) { _current = null; Refresh(); } });
        LoadDb(); CheckBios(); RefreshEmulators(); LoadPs3();
        if (OpticalDriveWatcher.CurrentDiscs().FirstOrDefault() is { } first) OnDisc(first);
        Refresh();
    }

    void LoadDb()
    {
        // PS1: libretro-database DAT (CC BY-SA 4.0) placed in DataDir by the user/manager; PS2: the installed PCSX2's GameIndex.yaml.
        var ps1 = Path.Combine(DataDir, "Sony - PlayStation.dat");
        if (File.Exists(ps1)) using (var r = File.OpenText(ps1)) _db.LoadLibretroDat(r, PsSystem.PS1);
        if (EmulatorLocator.Find().Pcsx2Exe is { } p && Path.Combine(Path.GetDirectoryName(p)!, "resources", "GameIndex.yaml") is var gi && File.Exists(gi))
            using (var r = File.OpenText(gi)) _db.LoadPcsx2GameIndex(r);
    }

    void OnDisc(string drive)
    {
        try { _current = new DiscIdentifier(_db).Identify(new DriveDiscReader(drive)); }
        catch (IOException) { _current = null; }
        Refresh();
        if (_current?.System is PsSystem.PS1 or PsSystem.PS2) _play.Focus(FocusState.Programmatic);
    }

    void Refresh()
    {
        _disc.Text = _current switch
        {
            null => "No disc",
            { System: PsSystem.PS3 } => "PS3 disc detected: play it from your own dump (no Blu-ray decryption here).",
            { System: PsSystem.Unknown } => "Not a PS1/PS2 game disc",
            var d => $"{d.System}: {d.Title ?? d.Serial} {(d.Serial is null ? "" : $"({d.Serial})")}",
        };
        _play.IsEnabled = _current?.System is PsSystem.PS1 or PsSystem.PS2;
        _copy.Visibility = _current?.System == PsSystem.PS1 ? Visibility.Visible : Visibility.Collapsed;
    }

    void PlayDisc(bool useImage, string? cue = null)
    {
        if (_current is null) return;
        var (spec, problem) = PlayStationLauncher.ForDisc(_current, EmulatorLocator.Find(), _ps1BiosOk, useImage, cue);
        if (spec is null) { _status.Text = problem switch { LaunchProblem.EmulatorMissing => "Install the emulator below first.", LaunchProblem.BiosMissing => "Choose a known-good PS1 BIOS first.", _ => problem.ToString() }; return; }
        _status.Text = "Starting…";
        var p = EmulatorLocator.Start(spec);
        // PS1 direct-disc CLI is unverified: if RetroArch quits within 8 s, suggest the image-copy fallback.
        if (_current.System == PsSystem.PS1 && !useImage)
            _ = Task.Run(async () => { var t0 = DateTime.Now; await p.WaitForExitAsync(); if (DateTime.Now - t0 < TimeSpan.FromSeconds(8)) DispatcherQueue.TryEnqueue(() => { _status.Text = "Direct disc play failed. Use “Copy PS1 disc to image, then play”."; _copy.Focus(FocusState.Programmatic); }); });
    }

    async Task CopyAndPlayAsync()
    {
        if (_current is not { System: PsSystem.PS1 } d) return;
        var dir = Path.Combine(DataDir, "Images"); var name = d.Serial ?? d.VolumeLabel ?? "disc";
        var cue = Path.Combine(dir, name + ".cue");
        if (!File.Exists(cue))
        {
            _copy.IsEnabled = false;
            try
            {
                using var raw = new WindowsRawCdReader(d.DrivePath);
                cue = await Task.Run(() => DiscImageCopy.CopyAsync(raw, dir, name, new Progress<double>(v => DispatcherQueue.TryEnqueue(() => _status.Text = $"Copying disc… {v:P0}"))));
            }
            catch (Exception ex) { _status.Text = $"Copy failed: {ex.Message}"; return; }
            finally { _copy.IsEnabled = true; }
        }
        PlayDisc(useImage: true, cue);
    }

    void CheckBios()
    {
        var f = Path.Combine(DataDir, "bios-ps1.txt");
        var path = File.Exists(f) ? File.ReadAllText(f).Trim() : null;
        var c = path is null ? null : new BiosValidator().Check(path);
        _ps1BiosOk = c?.IsKnownGood == true;
        _bios.Text = c is null ? "PS1 BIOS: not set (use your own console's dump)." :
            c.IsKnownGood ? $"PS1 BIOS OK: {c.Match!.Name}" : $"PS1 BIOS not recognised (md5 {c.Md5}). Beetle PSX needs scph5500/5501/5502.";
        if (_ps1BiosOk && EmulatorLocator.Find().RetroArchExe is { } ra)
        {
            var sys = Path.Combine(Path.GetDirectoryName(ra)!, "system"); Directory.CreateDirectory(sys);
            File.Copy(path!, Path.Combine(sys, Path.GetFileName(c!.Match!.Name.Split(' ')[0])), true);
        }
    }

    async Task PickBiosAsync()
    {
        var p = new FileOpenPicker(); WinRT.Interop.InitializeWithWindow.Initialize(p, WinRT.Interop.WindowNative.GetWindowHandle(_window));
        p.FileTypeFilter.Add(".bin"); p.FileTypeFilter.Add("*");
        if (await p.PickSingleFileAsync() is { } file) { File.WriteAllText(Path.Combine(DataDir, "bios-ps1.txt"), file.Path); CheckBios(); }
    }

    void RefreshEmulators()
    {
        var e = EmulatorLocator.Find();
        _emus.Text = $"RetroArch: {(e.RetroArchExe != null ? "✓" : "—")}   Beetle PSX: {(e.BeetleCore != null ? "✓" : "—")}   PCSX2: {(e.Pcsx2Exe != null ? "✓" : "—")}   RPCS3: {(e.Rpcs3Exe != null ? "✓" : "—")}";
        _emuButtons.Children.Clear();
        foreach (var k in new[] { EmulatorKind.RetroArch, EmulatorKind.BeetlePsxCore, EmulatorKind.Pcsx2, EmulatorKind.Rpcs3 })
        {
            var b = Btn($"Install/Update {k}"); b.Click += async (_, _) => await InstallAsync(k); _emuButtons.Children.Add(b);
        }
    }

    async Task InstallAsync(EmulatorKind k)
    {
        try
        {
            _status.Text = $"Checking {k}…";
            EmulatorRelease? rel = k == EmulatorKind.BeetlePsxCore ? EmulatorSources.BeetleCoreLatest() :
                EmulatorSources.ParseGitHubRelease(k, await _http.GetStringAsync(EmulatorSources.Official[k].ReleasesApi), EmulatorSources.Official[k].AssetPattern);
            if (rel is null) { _status.Text = "No official build found."; return; }
            var dlg = new ContentDialog { XamlRoot = XamlRoot, Title = $"Install {k} {rel.Version}?",
                Content = $"Download from the official source:\n{rel.DownloadUrl}\n\nVetTV doesn't bundle emulators. Their own licences (GPL) apply.",
                PrimaryButtonText = "Install", CloseButtonText = "Cancel", DefaultButton = ContentDialogButton.Close };
            if (await dlg.ShowAsync() != ContentDialogResult.Primary) { _status.Text = "Cancelled."; return; }
            var consent = new UserConsent(rel, DateTimeOffset.Now);
            var file = await new EmulatorInstaller(_http).DownloadAsync(consent, rel, Path.Combine(EmulatorLocator.Root, "_downloads"),
                new Progress<double>(v => _status.Text = $"Downloading {k}… {v:P0}"));
            var dest = k switch { EmulatorKind.BeetlePsxCore => Path.Combine(Path.GetDirectoryName(EmulatorLocator.Find().RetroArchExe ?? Path.Combine(EmulatorLocator.Root, "RetroArch", "retroarch.exe"))!, "cores"), _ => Path.Combine(EmulatorLocator.Root, k.ToString()) };
            await Task.Run(() => EmulatorLocator.Extract(file, dest));
            File.Delete(file);
            _status.Text = $"{k} {rel.Version} installed."; RefreshEmulators();
        }
        catch (Exception ex) { _status.Text = $"Install failed: {ex.GetType().Name}"; }
    }

    async Task AddPs3FolderAsync()
    {
        var p = new FolderPicker(); WinRT.Interop.InitializeWithWindow.Initialize(p, WinRT.Interop.WindowNative.GetWindowHandle(_window));
        p.FileTypeFilter.Add("*");
        if (await p.PickSingleFolderAsync() is { } f) { File.AppendAllText(Path.Combine(DataDir, "ps3-folders.txt"), f.Path + Environment.NewLine); LoadPs3(); }
    }

    void LoadPs3()
    {
        var list = Path.Combine(DataDir, "ps3-folders.txt");
        var roots = File.Exists(list) ? File.ReadAllLines(list).Where(l => l.Length > 0) : Enumerable.Empty<string>();
        var hdd = EmulatorLocator.Find().Rpcs3Exe is { } r ? Path.Combine(Path.GetDirectoryName(r)!, "dev_hdd0") : null;
        _ps3.ItemsSource = Ps3LibraryScanner.Scan(roots, hdd);
    }

    public static void LaunchPs3(GameEntry g)
    {
        if (g.Id.StartsWith("ps3:") && EmulatorLocator.Find().Rpcs3Exe is { } r) EmulatorLocator.Start(EmulatorLaunch.Rpcs3(r, g.LaunchUri));
    }

    public void Dispose() { _watcher.Dispose(); _http.Dispose(); }
    static TextBlock Text(double size, string t = "") => new() { Text = t, FontSize = size, Foreground = new SolidColorBrush(Microsoft.UI.Colors.White), TextWrapping = TextWrapping.Wrap };
    static Button Btn(string t) => new() { Content = t, Margin = new Thickness(0, 0, 8, 0) };
    static StackPanel Row(params UIElement[] e) { var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 }; foreach (var x in e) s.Children.Add(x); return s; }
}
