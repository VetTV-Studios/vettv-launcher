using Windows.Security.Credentials;
using VetTV.Launcher.Core.Security;
namespace VetTV.Launcher.App.Services;
/// Windows Credential Locker (per-user, DPAPI-protected by Windows). Tokens never touch logs or files.
public sealed class CredentialLockerSecretStore : ISecretStore
{
    readonly PasswordVault _vault = new();
    public void Save(string resource, string account, string secret) { Delete(resource, account); _vault.Add(new PasswordCredential($"VetTV:{resource}", account, secret)); }
    public string? Read(string resource, string account)
    {
        try { var c = _vault.Retrieve($"VetTV:{resource}", account); c.RetrievePassword(); return c.Password; }
        catch (Exception) { return null; }
    }
    public void Delete(string resource, string account)
    {
        try { _vault.Remove(_vault.Retrieve($"VetTV:{resource}", account)); } catch (Exception) { }
    }
}
