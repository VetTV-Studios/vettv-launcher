namespace VetTV.Launcher.Core.Pro;
public enum LicenseKind { Free, Trial, Full, TrialExpired }
public sealed record ProLicense(LicenseKind Kind, DateTimeOffset? TrialEndsAt = null)
{
    public bool HasPro => Kind is LicenseKind.Trial or LicenseKind.Full;
    public static ProLicense Free { get; } = new(LicenseKind.Free);
}
/// Pro features the open app can call. The open build ships FreeEdition; the closed Pro module
/// (not in the public repo) supplies the real implementation.
public interface IProEdition
{
    Task<ProLicense> GetLicenseAsync(CancellationToken ct = default);
    /// Opens the Store purchase UI for the Pro add-on. Returns the license afterwards.
    Task<ProLicense> PurchaseAsync(CancellationToken ct = default);
    /// null when shell-mode setup isn't available in this build.
    IShellModeSetup? ShellModeSetup { get; }
}
public interface IShellModeSetup
{
    /// Points HKCU Winlogon\Shell at the (open-source) watchdog exe. Requires a Pro license.
    void Enable(string watchdogExePath, ProLicense license);
}
public sealed class FreeEdition : IProEdition
{
    public Task<ProLicense> GetLicenseAsync(CancellationToken ct = default) => Task.FromResult(ProLicense.Free);
    public Task<ProLicense> PurchaseAsync(CancellationToken ct = default) => Task.FromResult(ProLicense.Free);
    public IShellModeSetup? ShellModeSetup => null;
}
