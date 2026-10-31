namespace VetTV.Launcher.Core.PlayStation;
public sealed record LaunchSpec(string FileName, IReadOnlyList<string> Args);
public enum Ps1Mode { PhysicalDisc, Image }
/// Command lines per documented CLIs (see docs/SPEC-playstation-hub.md §4).
public static class EmulatorLaunch
{
    /// PCSX2 CLI: https://pcsx2.net/docs/advanced/cli  (-disc <path>: host DVD drive as source)
    public static LaunchSpec Pcsx2Disc(string exe, string drive) => new(exe, new[] { "-fullscreen", "-batch", "-disc", DriveLetter(drive) });
    public static LaunchSpec Pcsx2Image(string exe, string image) => new(exe, new[] { "-fullscreen", "-batch", "--", image });

    /// RetroArch: -L core, -f fullscreen. Physical disc via the cdrom:// VFS scheme (UNVERIFIED CLI; menu "Load Disc" uses it).
    public static LaunchSpec RetroArchPs1(string exe, string corePath, Ps1Mode mode, string target) => new(exe, new[]
    {
        "-f", "-L", corePath,
        mode == Ps1Mode.PhysicalDisc ? $"cdrom://{DriveLetter(target).ToLowerInvariant()}/drive1.cue" : target,
    });

    /// RPCS3: --no-gui [--fullscreen] <EBOOT.BIN | %RPCS3_GAMEID%:TITLEID>. User dumps/PSN installs only.
    public static LaunchSpec Rpcs3(string exe, string ebootOrTitleId)
    {
        var target = ebootOrTitleId.EndsWith(".BIN", StringComparison.OrdinalIgnoreCase) ? ebootOrTitleId : $"%RPCS3_GAMEID%:{ebootOrTitleId}";
        return new(exe, new[] { "--no-gui", "--fullscreen", target });
    }

    /// "E:\", "E:", "e" => "E:"
    public static string DriveLetter(string drive)
    {
        var c = drive.TrimStart().FirstOrDefault();
        if (!char.IsAsciiLetter(c)) throw new ArgumentException("Not a drive letter", nameof(drive));
        return $"{char.ToUpperInvariant(c)}:";
    }
}
