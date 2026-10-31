using VetTV.Launcher.Core.Providers;
namespace VetTV.Launcher.Core.Library;
/// Aggregates providers; a failing provider never breaks the whole library.
public sealed class LibraryService
{
    readonly IEnumerable<ILibraryProvider> _providers;
    public LibraryService(IEnumerable<ILibraryProvider> providers) => _providers = providers;
    public List<string> Errors { get; } = new();

    public async Task<IReadOnlyList<GameEntry>> LoadAsync(CancellationToken ct = default)
    {
        Errors.Clear();
        var all = new Dictionary<string, GameEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in _providers)
        {
            try
            {
                if (!p.IsAvailable()) continue;
                foreach (var g in await p.GetGamesAsync(ct)) all.TryAdd(g.Id, g);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { Errors.Add($"{p.Source}: {ex.GetType().Name}"); }
        }
        return all.Values.OrderBy(g => g.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }
}
