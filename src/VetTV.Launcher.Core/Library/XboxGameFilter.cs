namespace VetTV.Launcher.Core.Library;
/// Platform-neutral description of an installed Store package; the App fills it from PackageManager.
public sealed record StorePackageInfo(string FamilyName, string DisplayName, string Aumid, string InstallPath,
    bool IsFramework, bool IsResource, bool HasMicrosoftGameConfig, bool IsSystemApp);
public static class XboxGameFilter
{
    /// MicrosoftGame.config in the install folder marks a GDK/PC Game Pass title.
    public static bool IsGame(StorePackageInfo p) =>
        !p.IsFramework && !p.IsResource && !p.IsSystemApp && p.HasMicrosoftGameConfig && !string.IsNullOrEmpty(p.Aumid);
    public static GameEntry ToEntry(StorePackageInfo p) =>
        new($"xbox:{p.FamilyName}", p.DisplayName, GameSource.Xbox, p.InstallPath, $"shell:AppsFolder\\{p.Aumid}");
}
