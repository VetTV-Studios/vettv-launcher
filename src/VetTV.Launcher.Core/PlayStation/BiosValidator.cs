using System.Security.Cryptography;
namespace VetTV.Launcher.Core.PlayStation;
public sealed record KnownBios(string Md5, PsSystem System, string Name);
public sealed record BiosCheck(string Path, string Md5, KnownBios? Match)
{
    public bool IsKnownGood => Match is not null;
}
/// Checks user-supplied BIOS dumps by MD5 against public known-good lists. Never bundles or downloads BIOS files.
public sealed class BiosValidator
{
    /// PS1 hashes from the libretro Beetle PSX docs BIOS table: https://docs.libretro.com/library/beetle_psx/#bios
    public static readonly IReadOnlyList<KnownBios> Ps1 = new KnownBios[]
    {
        new("8dd7d5296a650fac7319bce665a6a53c", PsSystem.PS1, "scph5500.bin (NTSC-J)"),
        new("490f666e1afb15b7362b406ed1cea246", PsSystem.PS1, "scph5501.bin (NTSC-U)"),
        new("32736f17079d0b2b7024407c39bd3050", PsSystem.PS1, "scph5502.bin (PAL)"),
    };
    readonly Dictionary<string, KnownBios> _known = new(StringComparer.OrdinalIgnoreCase);
    public BiosValidator(IEnumerable<KnownBios>? extra = null)
    {
        foreach (var k in Ps1.Concat(extra ?? Array.Empty<KnownBios>())) _known[k.Md5] = k;
    }
    /// Optional extra list (e.g. PS2) as "md5<TAB>system<TAB>name" lines, compiled by the user/maintainer from a public list.
    public static IEnumerable<KnownBios> ParseList(TextReader r)
    {
        string? l;
        while ((l = r.ReadLine()) != null)
        {
            var p = l.Split('\t'); if (p.Length < 3 || l.StartsWith('#')) continue;
            if (p[0].Length == 32 && Enum.TryParse<PsSystem>(p[1], true, out var s)) yield return new KnownBios(p[0].ToLowerInvariant(), s, p[2]);
        }
    }
    public BiosCheck Check(string path)
    {
        var fi = new FileInfo(path);
        if (!fi.Exists || fi.Length > 16 * 1024 * 1024) return new BiosCheck(path, "", null); // BIOS dumps are 512 KB–4 MB
        using var f = fi.OpenRead();
        var md5 = Convert.ToHexString(MD5.HashData(f)).ToLowerInvariant();
        return new BiosCheck(path, md5, _known.GetValueOrDefault(md5));
    }
}
