namespace VetTV.Launcher.Core.Platform;
/// HKCU-only registry abstraction so shell/startup logic is unit-testable off Windows.
public interface IRegistryStore
{
    string? GetValue(string keyPath, string name);
    void SetValue(string keyPath, string name, string value);
    void DeleteValue(string keyPath, string name);
}
public sealed class InMemoryRegistryStore : IRegistryStore
{
    public Dictionary<(string, string), string> Values { get; } = new();
    public string? GetValue(string k, string n) => Values.TryGetValue((k, n), out var v) ? v : null;
    public void SetValue(string k, string n, string v) => Values[(k, n)] = v;
    public void DeleteValue(string k, string n) => Values.Remove((k, n));
}
