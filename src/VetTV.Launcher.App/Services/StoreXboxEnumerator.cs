using Windows.Management.Deployment;
using VetTV.Launcher.Core.Library;
namespace VetTV.Launcher.App.Services;
public static class StoreXboxEnumerator
{
    public static async Task<IEnumerable<StorePackageInfo>> EnumerateAsync(CancellationToken ct)
    {
        var list = new List<StorePackageInfo>();
        foreach (var pkg in new PackageManager().FindPackagesForUser(string.Empty))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var path = pkg.InstalledLocation?.Path ?? "";
                var entries = await pkg.GetAppListEntriesAsync();
                var entry = entries.FirstOrDefault();
                list.Add(new StorePackageInfo(pkg.Id.FamilyName, entry?.DisplayInfo.DisplayName ?? pkg.DisplayName,
                    entry?.AppUserModelId ?? "", path, pkg.IsFramework, pkg.IsResourcePackage,
                    File.Exists(Path.Combine(path, "MicrosoftGame.config")), pkg.SignatureKind == Windows.ApplicationModel.PackageSignatureKind.System));
            }
            catch (Exception) { /* package without access/location: skip */ }
        }
        return list;
    }
}
