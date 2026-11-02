using System.Runtime.Versioning;
using Microsoft.Win32;
namespace VetTV.Launcher.Core.Platform;
/// Read-only HKLM access so store providers (GOG) stay testable off Windows.
public interface IMachineRegistry
{
    IReadOnlyList<string> GetSubKeyNames(string keyPath);
    string? GetValue(string keyPath, string name);
}
public sealed class InMemoryMachineRegistry : IMachineRegistry
{
    public Dictionary<string, Dictionary<string, string>> Keys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public void Set(string key, string name, string value)
    {
        if (!Keys.TryGetValue(key, out var d)) Keys[key] = d = new(StringComparer.OrdinalIgnoreCase);
        d[name] = value;
    }
    public IReadOnlyList<string> GetSubKeyNames(string keyPath)
    {
        var pre = keyPath.TrimEnd('\\') + "\\";
        return Keys.Keys.Where(k => k.StartsWith(pre, StringComparison.OrdinalIgnoreCase))
            .Select(k => k[pre.Length..].Split('\\')[0]).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
    public string? GetValue(string k, string n) => Keys.TryGetValue(k, out var d) && d.TryGetValue(n, out var v) ? v : null;
}
[SupportedOSPlatform("windows")]
public sealed class WindowsMachineRegistry : IMachineRegistry
{
    public IReadOnlyList<string> GetSubKeyNames(string k) { using var key = Registry.LocalMachine.OpenSubKey(k); return key?.GetSubKeyNames() ?? Array.Empty<string>(); }
    public string? GetValue(string k, string n) { using var key = Registry.LocalMachine.OpenSubKey(k); return key?.GetValue(n)?.ToString(); }
}
