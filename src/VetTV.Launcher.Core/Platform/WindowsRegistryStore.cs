using System.Runtime.Versioning;
using Microsoft.Win32;
namespace VetTV.Launcher.Core.Platform;
[SupportedOSPlatform("windows")]
public sealed class WindowsRegistryStore : IRegistryStore
{
    public string? GetValue(string k, string n) { using var key = Registry.CurrentUser.OpenSubKey(k); return key?.GetValue(n) as string; }
    public void SetValue(string k, string n, string v) { using var key = Registry.CurrentUser.CreateSubKey(k); key.SetValue(n, v, RegistryValueKind.String); }
    public void DeleteValue(string k, string n) { using var key = Registry.CurrentUser.OpenSubKey(k, writable: true); key?.DeleteValue(n, throwOnMissingValue: false); }
}
