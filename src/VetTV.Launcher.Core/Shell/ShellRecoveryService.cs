using VetTV.Launcher.Core.Platform;
namespace VetTV.Launcher.Core.Shell;
public enum ShellMode { Normal, RegistryShell }
/// OPEN-SOURCE on purpose: detecting and undoing VetTV shell mode must always be available,
/// even without Pro, so nobody can get stuck without a desktop. (Turning shell mode ON is Pro.)
public sealed class ShellRecoveryService
{
    public const string WinlogonKey = @"Software\Microsoft\Windows NT\CurrentVersion\Winlogon";
    public const string VetTvKey = @"Software\VetTV\Launcher";
    public const string PreviousShellValue = "PreviousShell";
    readonly IRegistryStore _reg;
    public ShellRecoveryService(IRegistryStore reg) => _reg = reg;

    public ShellMode Current =>
        _reg.GetValue(WinlogonKey, "Shell") is { } s && s.Contains("VetTV", StringComparison.OrdinalIgnoreCase)
            ? ShellMode.RegistryShell : ShellMode.Normal;

    /// Restores the user's previous per-user shell, or deletes the HKCU value so HKLM's explorer.exe applies.
    public void RestoreExplorerShell()
    {
        var prev = _reg.GetValue(VetTvKey, PreviousShellValue);
        if (!string.IsNullOrEmpty(prev) && !prev.Contains("VetTV", StringComparison.OrdinalIgnoreCase))
            _reg.SetValue(WinlogonKey, "Shell", prev);
        else
            _reg.DeleteValue(WinlogonKey, "Shell");
        _reg.DeleteValue(VetTvKey, PreviousShellValue);
    }
}
