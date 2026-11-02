namespace VetTV.Launcher.Core.Providers;
public abstract class StubProvider : ILibraryProvider
{
    public abstract GameSource Source { get; }
    public virtual bool IsAvailable() => false;
    public Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GameEntry>>(Array.Empty<GameEntry>());
}
/// TODO M2: scan user-picked folders by extension, launch via configured emulator command line.
public sealed class EmulatorProvider : StubProvider { public override GameSource Source => GameSource.Emulator; }
