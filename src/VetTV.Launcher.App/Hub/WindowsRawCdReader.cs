using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using VetTV.Launcher.Core.PlayStation;
namespace VetTV.Launcher.App.Hub;
/// Raw sector reads for the PS1 image-copy fallback: IOCTL_CDROM_READ_TOC + IOCTL_CDROM_RAW_READ (2352-byte sectors).
/// Reads only; no copy-protection circumvention or key handling.
public sealed class WindowsRawCdReader : IRawCdReader, IDisposable
{
    const uint IOCTL_CDROM_READ_TOC = 0x00024000, IOCTL_CDROM_RAW_READ = 0x0002403E;
    const int YellowMode2 = 0, XAForm2 = 1, CDDA = 2;
    readonly SafeFileHandle _h;
    readonly HashSet<int> _audioStarts = new(); List<CdTrack> _tracks = new();
    public WindowsRawCdReader(string drive)
    {
        _h = CreateFile($@"\\.\{EmulatorLaunch.DriveLetter(drive)}", 0x80000000 /*GENERIC_READ*/, 3, IntPtr.Zero, 3, 0, IntPtr.Zero);
        if (_h.IsInvalid) throw new IOException($"Cannot open drive (error {Marshal.GetLastWin32Error()})");
    }
    public IReadOnlyList<CdTrack> ReadToc(out int leadOutLba)
    {
        var buf = new byte[804];
        if (!DeviceIoControl(_h, IOCTL_CDROM_READ_TOC, IntPtr.Zero, 0, buf, buf.Length, out _, IntPtr.Zero))
            throw new IOException($"READ_TOC failed ({Marshal.GetLastWin32Error()})");
        int first = buf[2], last = buf[3]; _tracks = new(); leadOutLba = 0;
        for (int i = 0; i <= last - first + 1; i++)
        {
            int o = 4 + i * 8; int ctrl = buf[o + 1] >> 4; int num = buf[o + 2];
            int lba = (buf[o + 5] * 60 + buf[o + 6]) * 75 + buf[o + 7] - 150; // MSF addressing
            if (num == 0xAA) { leadOutLba = lba; break; }
            bool audio = (ctrl & 0x4) == 0; _tracks.Add(new CdTrack(num, lba, audio)); if (audio) _audioStarts.Add(lba);
        }
        return _tracks;
    }
    public int ReadSectors(int lba, int count, Span<byte> buffer)
    {
        bool audio = _tracks.LastOrDefault(t => t.StartLba <= lba)?.IsAudio ?? false;
        var info = new byte[16];
        BitConverter.GetBytes((long)lba * 2048).CopyTo(info, 0);
        BitConverter.GetBytes(count).CopyTo(info, 8);
        BitConverter.GetBytes(audio ? CDDA : XAForm2).CopyTo(info, 12);
        var outBuf = new byte[count * DiscImageCopy.SectorSize];
        if (!DeviceIoControl(_h, IOCTL_CDROM_RAW_READ, info, info.Length, outBuf, outBuf.Length, out var got, IntPtr.Zero)) return 0;
        outBuf.AsSpan(0, got).CopyTo(buffer);
        return got / DiscImageCopy.SectorSize;
    }
    public void Dispose() => _h.Dispose();
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    static extern SafeFileHandle CreateFile(string n, uint a, uint s, IntPtr sa, uint c, uint f, IntPtr t);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle h, uint code, byte[] inBuf, int inSize, byte[] outBuf, int outSize, out int ret, IntPtr ov);
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool DeviceIoControl(SafeFileHandle h, uint code, IntPtr inBuf, int inSize, byte[] outBuf, int outSize, out int ret, IntPtr ov);
}
