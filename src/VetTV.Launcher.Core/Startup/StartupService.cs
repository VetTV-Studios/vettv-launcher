using VetTV.Launcher.Core.Platform;
namespace VetTV.Launcher.Core.Startup;
/// "Start on sign-in" for the unpackaged build: HKCU Run key. (Packaged MSIX build uses a StartupTask instead.)
public sealed class StartupService
{
    public const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public const string ValueName = "VetTVLauncher";
    readonly IRegistryStore _reg;
    public StartupService(IRegistryStore reg) => _reg = reg;
    public bool IsEnabled => _reg.GetValue(RunKey, ValueName) is not null;
    public void Enable(string exePath) => _reg.SetValue(RunKey, ValueName, $"\"{exePath}\" --startup");
    public void Disable() => _reg.DeleteValue(RunKey, ValueName);
}
