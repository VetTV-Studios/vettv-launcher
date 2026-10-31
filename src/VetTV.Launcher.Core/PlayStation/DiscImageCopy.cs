using System.Text;
namespace VetTV.Launcher.Core.PlayStation;
public sealed record CdTrack(int Number, int StartLba, bool IsAudio);
/// Raw 2352-byte sector access to a physical CD (App: IOCTL_CDROM_READ_TOC + IOCTL_CDROM_RAW_READ).
public interface IRawCdReader
{
    IReadOnlyList<CdTrack> ReadToc(out int leadOutLba);
    int ReadSectors(int lba, int count, Span<byte> buffer); // returns sectors read; buffer = count*2352
}
/// PS1 fallback: copy the user's own disc to a single .bin + .cue (MODE2/2352 data + AUDIO tracks), then play the image.
public static class DiscImageCopy
{
    public const int SectorSize = 2352;
    public static string BuildCue(string binFileName, IReadOnlyList<CdTrack> tracks)
    {
        var sb = new StringBuilder().Append($"FILE \"{binFileName}\" BINARY\n");
        foreach (var t in tracks)
        {
            sb.Append($"  TRACK {t.Number:00} {(t.IsAudio ? "AUDIO" : "MODE2/2352")}\n");
            if (t.IsAudio && t.Number > 1) sb.Append($"    INDEX 00 {Msf(t.StartLba - 150 < 0 ? t.StartLba : t.StartLba - 150)}\n");
            sb.Append($"    INDEX 01 {Msf(t.StartLba)}\n");
        }
        return sb.ToString();
    }
    public static string Msf(int lba) => $"{lba / 75 / 60:00}:{lba / 75 % 60:00}:{lba % 75:00}";

    public static async Task<string> CopyAsync(IRawCdReader reader, string outDir, string baseName,
        IProgress<double>? progress = null, CancellationToken ct = default)
    {
        Directory.CreateDirectory(outDir);
        var tracks = reader.ReadToc(out var leadOut);
        if (tracks.Count == 0 || leadOut <= 0) throw new InvalidDataException("Empty or unreadable TOC");
        var safe = string.Concat(baseName.Split(Path.GetInvalidFileNameChars()));
        var bin = Path.Combine(outDir, safe + ".bin");
        var tmp = bin + ".part";
        const int chunk = 16;
        var buf = new byte[chunk * SectorSize];
        await using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write))
        {
            for (int lba = 0; lba < leadOut; lba += chunk)
            {
                ct.ThrowIfCancellationRequested();
                int n = Math.Min(chunk, leadOut - lba);
                int got = reader.ReadSectors(lba, n, buf.AsSpan(0, n * SectorSize));
                if (got != n) throw new IOException($"Read error at sector {lba}");
                await fs.WriteAsync(buf.AsMemory(0, n * SectorSize), ct);
                progress?.Report((double)(lba + n) / leadOut);
            }
        }
        File.Move(tmp, bin, overwrite: true);
        var cue = Path.Combine(outDir, safe + ".cue");
        await File.WriteAllTextAsync(cue, BuildCue(Path.GetFileName(bin), tracks), ct);
        return cue;
    }
}
