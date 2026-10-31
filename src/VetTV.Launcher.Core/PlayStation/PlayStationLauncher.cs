namespace VetTV.Launcher.Core.PlayStation;
public sealed record EmulatorPaths(string? RetroArchExe, string? BeetleCore, string? Pcsx2Exe, string? Rpcs3Exe);
public enum LaunchProblem { None, EmulatorMissing, BiosMissing, Ps3DiscNotSupported, UnknownDisc }
/// Picks emulator + args for an inserted disc or a PS3 library entry.
public static class PlayStationLauncher
{
    public static (LaunchSpec? Spec, LaunchProblem Problem) ForDisc(DiscInfo disc, EmulatorPaths p, bool ps1BiosOk, bool ps1UseImage = false, string? ps1ImageCue = null)
    {
        switch (disc.System)
        {
            case PsSystem.PS2:
                return p.Pcsx2Exe is null ? (null, LaunchProblem.EmulatorMissing) : (EmulatorLaunch.Pcsx2Disc(p.Pcsx2Exe, disc.DrivePath), LaunchProblem.None);
            case PsSystem.PS1:
                if (p.RetroArchExe is null || p.BeetleCore is null) return (null, LaunchProblem.EmulatorMissing);
                if (!ps1BiosOk) return (null, LaunchProblem.BiosMissing);
                return ps1UseImage && ps1ImageCue is not null
                    ? (EmulatorLaunch.RetroArchPs1(p.RetroArchExe, p.BeetleCore, Ps1Mode.Image, ps1ImageCue), LaunchProblem.None)
                    : (EmulatorLaunch.RetroArchPs1(p.RetroArchExe, p.BeetleCore, Ps1Mode.PhysicalDisc, disc.DrivePath), LaunchProblem.None);
            case PsSystem.PS3: return (null, LaunchProblem.Ps3DiscNotSupported); // no Blu-ray decryption; use your own dump
            default: return (null, LaunchProblem.UnknownDisc);
        }
    }
}
