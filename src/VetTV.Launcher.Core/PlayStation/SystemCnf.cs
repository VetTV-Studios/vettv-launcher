using System.Text.RegularExpressions;
namespace VetTV.Launcher.Core.PlayStation;
/// Parses SYSTEM.CNF. "BOOT2 = cdrom0:\SLUS_203.12;1" => PS2 / SLUS-20312; "BOOT = cdrom:\SCUS_944.26;1" => PS1 / SCUS-94426.
public static partial class SystemCnf
{
    [GeneratedRegex(@"^\s*(BOOT2?)\s*=\s*(\S+)", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex BootLine();
    [GeneratedRegex(@"([A-Z]{4})[_-](\d{3})\.?(\d{2})", RegexOptions.IgnoreCase)]
    private static partial Regex SerialPart();

    public static (PsSystem System, string? Serial) Parse(string text)
    {
        var m = BootLine().Match(text);
        if (!m.Success) return (PsSystem.Unknown, null);
        var sys = m.Groups[1].Value.Equals("BOOT2", StringComparison.OrdinalIgnoreCase) ? PsSystem.PS2 : PsSystem.PS1;
        var file = m.Groups[2].Value;
        var name = file[(file.LastIndexOfAny(new[] { '\\', '/', ':' }) + 1)..];
        var s = SerialPart().Match(name);
        return (sys, s.Success ? $"{s.Groups[1].Value.ToUpperInvariant()}-{s.Groups[2].Value}{s.Groups[3].Value}" : null);
    }

    /// Disc labels often carry the serial too (e.g. "SLUS_20312" or "SLUS20312").
    [GeneratedRegex(@"\b([A-Z]{4})[_-]?(\d{3})\.?(\d{2})\b", RegexOptions.IgnoreCase)]
    private static partial Regex LabelSerial();
    public static string? SerialFromLabel(string? label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;
        var m = LabelSerial().Match(label);
        return m.Success ? $"{m.Groups[1].Value.ToUpperInvariant()}-{m.Groups[2].Value}{m.Groups[3].Value}" : null;
    }
}
