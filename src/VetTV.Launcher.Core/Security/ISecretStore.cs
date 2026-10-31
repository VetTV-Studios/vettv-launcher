using System.Text.RegularExpressions;
namespace VetTV.Launcher.Core.Security;
/// Epic/GOG/Steam tokens & keys. Implemented in the App with Windows Credential Locker (PasswordVault),
/// DPAPI (CurrentUser) as fallback. Never in code, config files in plain text, or logs.
public interface ISecretStore
{
    void Save(string resource, string account, string secret);
    string? Read(string resource, string account);
    void Delete(string resource, string account);
}
public static partial class LogRedactor
{
    [GeneratedRegex(@"(?i)(access_token|refresh_token|authorization|api[_-]?key|key|token|password|code)(\s*[=:]\s*|""\s*:\s*"")(Bearer\s+)?[^\s&""',;]+")]
    private static partial Regex SecretPattern();
    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9\-._~+/]+=*")]
    private static partial Regex BearerPattern();
    public static string Redact(string? message) =>
        message is null ? "" : BearerPattern().Replace(SecretPattern().Replace(message, m => m.Groups[1].Value + m.Groups[2].Value + "***"), "Bearer ***");
}
