using System.Diagnostics;
using VetTV.Launcher.Core.PlayStation;
namespace VetTV.Launcher.App.Hub;
/// Emulators live under %LOCALAPPDATA%\VetTV\Emulators\<name> (installed by the manager) or a user-chosen path.
public static class EmulatorLocator
{
    public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VetTV", "Emulators");
    public static EmulatorPaths Find()
    {
        string? F(params string[] rel) => rel.Select(r => Path.Combine(Root, r)).FirstOrDefault(File.Exists);
        var ra = F(@"RetroArch\RetroArch-Win64\retroarch.exe", @"RetroArch\retroarch.exe");
        var core = ra is null ? null : new[] { "mednafen_psx_hw_libretro.dll", "mednafen_psx_libretro.dll" }
            .Select(c => Path.Combine(Path.GetDirectoryName(ra)!, "cores", c)).FirstOrDefault(File.Exists);
        return new EmulatorPaths(ra, core, F(@"PCSX2\pcsx2-qt.exe"), F(@"RPCS3\rpcs3.exe"));
    }
    /// Extracts .zip with .NET; .7z with Windows 11's built-in tar (libarchive). VERIFY 7z support on build 26340.
    public static void Extract(string archive, string destDir)
    {
        Directory.CreateDirectory(destDir);
        if (archive.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)) { System.IO.Compression.ZipFile.ExtractToDirectory(archive, destDir, true); return; }
        var tar = Path.Combine(Environment.SystemDirectory, "tar.exe");
        var psi = new ProcessStartInfo(tar) { UseShellExecute = false, CreateNoWindow = true };
        psi.ArgumentList.Add("-xf"); psi.ArgumentList.Add(archive); psi.ArgumentList.Add("-C"); psi.ArgumentList.Add(destDir);
        using var p = Process.Start(psi)!; p.WaitForExit();
        if (p.ExitCode != 0) throw new IOException($"Extraction failed ({p.ExitCode}). Install 7-Zip or extract manually.");
    }
    public static Process Start(LaunchSpec s)
    {
        var psi = new ProcessStartInfo(s.FileName) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(s.FileName)! };
        foreach (var a in s.Args) psi.ArgumentList.Add(a);
        return Process.Start(psi)!;
    }
}
