namespace VetTV.Launcher.Core.PlayStation;
public enum PsSystem { Unknown, PS1, PS2, PS3 }
public sealed record DiscInfo(PsSystem System, string? Serial, string? Title, string? VolumeLabel, string DrivePath);
