namespace VetTV.Launcher.Core.PlayStation;
/// Read-only view of an inserted disc (App: drive letter; tests: fakes).
public interface IDiscReader
{
    string DrivePath { get; }               // e.g. "E:\"
    string? VolumeLabel { get; }
    long TotalBytes { get; }                // > ~900 MB => DVD
    string? ReadTextFile(string relativePath);
    bool FileExists(string relativePath);
}
public sealed class DiscIdentifier
{
    readonly GameDb _db;
    public DiscIdentifier(GameDb db) => _db = db;
    const long CdMaxBytes = 900L * 1024 * 1024;

    public DiscInfo Identify(IDiscReader d)
    {
        // PS3 discs (PS3_DISC.SFB) are NOT played from disc: no Blu-ray decryption/keys. Report and stop.
        if (d.FileExists("PS3_DISC.SFB")) return new DiscInfo(PsSystem.PS3, null, null, d.VolumeLabel, d.DrivePath);

        var (sys, serial) = d.ReadTextFile("SYSTEM.CNF") is { } cnf ? SystemCnf.Parse(cnf) : (PsSystem.Unknown, null);
        serial ??= SystemCnf.SerialFromLabel(d.VolumeLabel);
        if (sys == PsSystem.Unknown)
        {
            if (serial is not null && _db.Lookup(serial) is { } e) sys = e.System;
            else if (d.FileExists("PSX.EXE")) sys = PsSystem.PS1;            // early PS1 discs without SYSTEM.CNF
            else if (serial is not null) sys = d.TotalBytes > CdMaxBytes ? PsSystem.PS2 : PsSystem.PS1;
        }
        var title = serial is null ? null : _db.Lookup(serial)?.Title;
        return new DiscInfo(sys, serial, title ?? (sys != PsSystem.Unknown ? d.VolumeLabel ?? serial : null), d.VolumeLabel, d.DrivePath);
    }
}
