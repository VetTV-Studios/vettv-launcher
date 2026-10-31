namespace VetTV.Launcher.Core.Providers;
public interface ILibraryProvider
{
    GameSource Source { get; }
    bool IsAvailable();
    Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default);
}
