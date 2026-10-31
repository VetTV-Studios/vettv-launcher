using VetTV.Launcher.Core.PlayStation;
namespace VetTV.Launcher.App.Hub;
/// Reads the mounted ISO 9660 filesystem of a PS1/PS2 disc through its drive letter (read-only).
public sealed class DriveDiscReader(string root) : IDiscReader
{
    readonly DriveInfo _d = new(root);
    public string DrivePath => root;
    public string? VolumeLabel => _d.IsReady ? _d.VolumeLabel : null;
    public long TotalBytes => _d.IsReady ? _d.TotalSize : 0;
    public bool FileExists(string p) => File.Exists(Path.Combine(root, p));
    public string? ReadTextFile(string p)
    {
        var f = Path.Combine(root, p);
        if (!File.Exists(f) || new FileInfo(f).Length > 64 * 1024) return null;
        return File.ReadAllText(f);
    }
}
