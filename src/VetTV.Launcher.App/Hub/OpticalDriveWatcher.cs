using System.Runtime.InteropServices;
using WinRT.Interop;
namespace VetTV.Launcher.App.Hub;
/// Raises DiscInserted/DiscRemoved from WM_DEVICECHANGE (DBT_DEVICEARRIVAL / DBT_DEVICEREMOVECOMPLETE, DBT_DEVTYP_VOLUME + DBTF_MEDIA).
public sealed class OpticalDriveWatcher : IDisposable
{
    const uint WM_DEVICECHANGE = 0x0219; const int DBT_DEVICEARRIVAL = 0x8000, DBT_DEVICEREMOVECOMPLETE = 0x8004, DBT_DEVTYP_VOLUME = 2;
    readonly IntPtr _hwnd; readonly SubclassProc _proc;
    public event Action<string>? DiscInserted, DiscRemoved;
    public OpticalDriveWatcher(Microsoft.UI.Xaml.Window w)
    {
        _hwnd = WindowNative.GetWindowHandle(w); _proc = WndProc;
        SetWindowSubclass(_hwnd, _proc, 2, IntPtr.Zero);
    }
    /// Drives that already hold a disc at startup.
    public static IEnumerable<string> CurrentDiscs() =>
        DriveInfo.GetDrives().Where(d => d.DriveType == DriveType.CDRom && d.IsReady).Select(d => d.RootDirectory.FullName);

    IntPtr WndProc(IntPtr h, uint msg, IntPtr wp, IntPtr lp, IntPtr id, IntPtr data)
    {
        if (msg == WM_DEVICECHANGE && (wp == DBT_DEVICEARRIVAL || wp == DBT_DEVICEREMOVECOMPLETE) && lp != IntPtr.Zero
            && Marshal.ReadInt32(lp, 4) == DBT_DEVTYP_VOLUME)
        {
            var v = Marshal.PtrToStructure<DEV_BROADCAST_VOLUME>(lp);
            for (int i = 0; i < 26; i++)
            {
                if ((v.dbcv_unitmask & (1u << i)) == 0) continue;
                var root = $"{(char)('A' + i)}:\\";
                if (new DriveInfo(root).DriveType != DriveType.CDRom) continue;
                if (wp == DBT_DEVICEARRIVAL) DiscInserted?.Invoke(root); else DiscRemoved?.Invoke(root);
            }
        }
        return DefSubclassProc(h, msg, wp, lp);
    }
    public void Dispose() => RemoveWindowSubclass(_hwnd, _proc, 2);

    [StructLayout(LayoutKind.Sequential)] struct DEV_BROADCAST_VOLUME { public int dbcv_size, dbcv_devicetype, dbcv_reserved; public uint dbcv_unitmask; public ushort dbcv_flags; }
    delegate IntPtr SubclassProc(IntPtr h, uint m, IntPtr w, IntPtr l, IntPtr id, IntPtr d);
    [DllImport("comctl32.dll")] static extern bool SetWindowSubclass(IntPtr h, SubclassProc p, nuint id, IntPtr d);
    [DllImport("comctl32.dll")] static extern bool RemoveWindowSubclass(IntPtr h, SubclassProc p, nuint id);
    [DllImport("comctl32.dll")] static extern IntPtr DefSubclassProc(IntPtr h, uint m, IntPtr w, IntPtr l);
}
