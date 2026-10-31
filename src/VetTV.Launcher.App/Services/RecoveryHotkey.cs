using System.Runtime.InteropServices;
using WinRT.Interop;
namespace VetTV.Launcher.App.Services;
/// Global Ctrl+Alt+Shift+F12 => Exit to Desktop (works even if the UI is stuck on focus).
public sealed class RecoveryHotkey : IDisposable
{
    const int Id = 0x5654; const uint MOD_ALT = 1, MOD_CONTROL = 2, MOD_SHIFT = 4, MOD_NOREPEAT = 0x4000, VK_F12 = 0x7B, WM_HOTKEY = 0x0312;
    readonly IntPtr _hwnd; readonly Action _onPressed; readonly SubclassProc _proc;
    public RecoveryHotkey(Microsoft.UI.Xaml.Window w, Action onPressed)
    {
        _hwnd = WindowNative.GetWindowHandle(w); _onPressed = onPressed;
        _proc = WndProc;
        SetWindowSubclass(_hwnd, _proc, 1, IntPtr.Zero);
        RegisterHotKey(_hwnd, Id, MOD_CONTROL | MOD_ALT | MOD_SHIFT | MOD_NOREPEAT, VK_F12);
    }
    IntPtr WndProc(IntPtr h, uint msg, IntPtr wp, IntPtr lp, IntPtr id, IntPtr data)
    {
        if (msg == WM_HOTKEY && wp == Id) _onPressed();
        return DefSubclassProc(h, msg, wp, lp);
    }
    public void Dispose() { UnregisterHotKey(_hwnd, Id); RemoveWindowSubclass(_hwnd, _proc, 1); }
    delegate IntPtr SubclassProc(IntPtr h, uint m, IntPtr w, IntPtr l, IntPtr id, IntPtr d);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mod, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("comctl32.dll")] static extern bool SetWindowSubclass(IntPtr h, SubclassProc p, nuint id, IntPtr d);
    [DllImport("comctl32.dll")] static extern bool RemoveWindowSubclass(IntPtr h, SubclassProc p, nuint id);
    [DllImport("comctl32.dll")] static extern IntPtr DefSubclassProc(IntPtr h, uint m, IntPtr w, IntPtr l);
}
