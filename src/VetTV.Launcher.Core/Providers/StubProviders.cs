namespace VetTV.Launcher.Core.Providers;
public abstract class StubProvider : ILibraryProvider
{
    public abstract GameSource Source { get; }
    public virtual bool IsAvailable() => false;
    public Task<IReadOnlyList<GameEntry>> GetGamesAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<GameEntry>>(Array.Empty<GameEntry>());
}
/// TODO M2: parse %ProgramData%\Epic\EpicGamesLauncher\Data\Manifests\*.item; launch com.epicgames.launcher://apps/{AppName}?action=launch
public sealed class EpicProvider : StubProvider { public override GameSource Source => GameSource.Epic; }
/// TODO M2: HKLM\SOFTWARE\WOW6432Node\GOG.com\Games registry keys; Galaxy DB later.
public sealed class GogProvider : StubProvider { public override GameSource Source => GameSource.Gog; }
/// TODO M2: scan user-picked folders by extension, launch via configured emulator command line.
public sealed class EmulatorProvider : StubProvider { public override GameSource Source => GameSource.Emulator; }
