using VetTV.Launcher.Core.Library;
namespace VetTV.Launcher.Core.Providers;
/// Xbox/Store installed games. Package enumeration is injected (WinRT PackageManager lives in the App).
public sealed class XboxProvider : ILibraryProvider
{
    readonly Func<CancellationToken, Task<IEnumerable<StorePackageInfo>>>? _enumerate;
    public XboxProvider(Func<CancellationToken, Task<IEnumerable<StorePackageInfo>>>? enumerate = null) => _enumerate = enumerate;
    public GameSource Source => GameSource.Xbox;
    public bool IsAvailable() => _enumerate is not null;
    public async Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default)
    {
        if (_enumerate is null) return Array.Empty<GameEntry>();
        return (await _enumerate(ct)).Where(XboxGameFilter.IsGame).Select(XboxGameFilter.ToEntry).ToList();
    }
}
