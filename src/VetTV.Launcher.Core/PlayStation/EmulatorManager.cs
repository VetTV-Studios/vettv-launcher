using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace VetTV.Launcher.Core.PlayStation;
public enum EmulatorKind { RetroArch, BeetlePsxCore, Pcsx2, Rpcs3 }
public sealed record EmulatorSource(EmulatorKind Kind, string ReleasesApi, string AssetPattern, string Homepage);
public sealed record EmulatorRelease(EmulatorKind Kind, string Version, string DownloadUrl, string AssetName, long Size, string? Sha256);
/// Consent token: only created by the UI after the user pressed "Install"/"Update" for this exact release.
public sealed record UserConsent(EmulatorRelease Release, DateTimeOffset GivenAt);

/// Finds/updates emulators from OFFICIAL sources only, and only with explicit per-action user consent.
/// Nothing is bundled in our package. (Store builds don't include the hub at all.)
public static class EmulatorSources
{
    public static readonly IReadOnlyDictionary<EmulatorKind, EmulatorSource> Official = new Dictionary<EmulatorKind, EmulatorSource>
    {
        [EmulatorKind.Pcsx2] = new(EmulatorKind.Pcsx2, "https://api.github.com/repos/PCSX2/pcsx2/releases/latest", @"windows-x64-Qt\.7z$", "https://pcsx2.net"),
        [EmulatorKind.Rpcs3] = new(EmulatorKind.Rpcs3, "https://api.github.com/repos/RPCS3/rpcs3-binaries-win/releases/latest", @"win64.*\.7z$", "https://rpcs3.net"),
        [EmulatorKind.RetroArch] = new(EmulatorKind.RetroArch, "https://api.github.com/repos/libretro/RetroArch/releases/latest", "", "https://www.retroarch.com"),
        [EmulatorKind.BeetlePsxCore] = new(EmulatorKind.BeetlePsxCore, "", "", "https://docs.libretro.com/library/beetle_psx_hw/"),
    };
    static readonly string[] AllowedHosts =
    {
        "github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com", "api.github.com", "buildbot.libretro.com",
    };
    static readonly string[] AllowedGitHubRepos = { "/PCSX2/pcsx2/", "/RPCS3/rpcs3-binaries-win/", "/libretro/RetroArch/" };

    public static bool IsOfficialUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps) return false;
        if (!AllowedHosts.Contains(u.Host, StringComparer.OrdinalIgnoreCase)) return false;
        if (u.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
            return AllowedGitHubRepos.Any(r => u.AbsolutePath.StartsWith(r, StringComparison.OrdinalIgnoreCase));
        return true;
    }

    /// RetroArch stable build + Beetle PSX HW core from libretro's buildbot (official).
    public static EmulatorRelease RetroArchFromTag(string tag)
    {
        var v = tag.TrimStart('v');
        return new(EmulatorKind.RetroArch, v, $"https://buildbot.libretro.com/stable/{v}/windows/x86_64/RetroArch.7z", "RetroArch.7z", 0, null);
    }
    public static EmulatorRelease BeetleCoreLatest() =>
        new(EmulatorKind.BeetlePsxCore, "nightly", "https://buildbot.libretro.com/nightly/windows/x86_64/latest/mednafen_psx_hw_libretro.dll.zip", "mednafen_psx_hw_libretro.dll.zip", 0, null);

    /// Parses a GitHub "releases/latest" JSON and picks the asset matching the pattern.
    public static EmulatorRelease? ParseGitHubRelease(EmulatorKind kind, string json, string assetPattern)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (kind == EmulatorKind.RetroArch) return RetroArchFromTag(tag);
        if (!root.TryGetProperty("assets", out var assets)) return null;
        var rx = new Regex(assetPattern, RegexOptions.IgnoreCase);
        foreach (var a in assets.EnumerateArray())
        {
            var name = a.GetProperty("name").GetString() ?? "";
            var url = a.GetProperty("browser_download_url").GetString() ?? "";
            if (!rx.IsMatch(name) || !IsOfficialUrl(url)) continue;
            string? sha = a.TryGetProperty("digest", out var dg) && dg.GetString() is { } s && s.StartsWith("sha256:") ? s[7..] : null;
            return new(kind, tag.TrimStart('v'), url, name, a.TryGetProperty("size", out var sz) ? sz.GetInt64() : 0, sha);
        }
        return null;
    }

    public static bool IsNewer(string current, string candidate) =>
        Version.TryParse(Clean(candidate), out var c) && (!Version.TryParse(Clean(current), out var cur) || c > cur);
    static string Clean(string v) => Regex.Match(v, @"\d+(\.\d+){1,3}").Value;
}

public sealed class EmulatorInstaller
{
    readonly HttpClient _http;
    public EmulatorInstaller(HttpClient http) => _http = http;

    /// Downloads to <downloadDir>. Refuses without consent for this exact release or for non-official URLs.
    public async Task<string> DownloadAsync(UserConsent? consent, EmulatorRelease release, string downloadDir,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        if (consent is null || consent.Release != release) throw new UnauthorizedAccessException("User consent required for this download.");
        if (!EmulatorSources.IsOfficialUrl(release.DownloadUrl)) throw new InvalidOperationException("Not an official source.");
        Directory.CreateDirectory(downloadDir);
        var path = Path.Combine(downloadDir, Path.GetFileName(release.AssetName));
        using var resp = await _http.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();
        if (resp.RequestMessage?.RequestUri is { } final && !EmulatorSources.IsOfficialUrl(final.ToString()))
            throw new InvalidOperationException("Redirected to a non-official host.");
        var total = resp.Content.Headers.ContentLength ?? release.Size;
        await using (var src = await resp.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(path))
        {
            var buf = new byte[81920]; long done = 0; int n;
            while ((n = await src.ReadAsync(buf, ct)) > 0)
            {
                await dst.WriteAsync(buf.AsMemory(0, n), ct); done += n;
                if (total > 0) progress?.Report((double)done / total);
            }
        }
        if (release.Sha256 is { } expected)
        {
            await using var f = File.OpenRead(path);
            var actual = Convert.ToHexString(await SHA256.HashDataAsync(f, ct));
            if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase)) { File.Delete(path); throw new InvalidDataException("Checksum mismatch."); }
        }
        return path;
    }
}
