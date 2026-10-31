namespace VetTV.Launcher.Core.Overlay;
public interface IBrightnessControl { bool IsSupported { get; } int Get(); void Set(int percent); }
public interface IWifiControl { bool IsSupported { get; } Task<IReadOnlyList<string>> ScanAsync(); Task<bool> ConnectAsync(string ssid); }
/// AMD: RyzenAdj (bundled as separate LGPL binary, invoked as process). Intel/NVIDIA: power modes only.
public interface ITdpControl { bool IsSupported { get; } int? CurrentWatts { get; } bool SetWatts(int watts); }
