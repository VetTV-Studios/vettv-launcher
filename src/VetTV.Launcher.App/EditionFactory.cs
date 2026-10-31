using VetTV.Launcher.Core.Platform;
using VetTV.Launcher.Core.Pro;
namespace VetTV.Launcher.App;
/// Returns FreeEdition unless an optional partial implementation supplies another edition.
public static partial class EditionFactory
{
    static partial void CreateProEdition(IntPtr hwnd, IRegistryStore reg, ref IProEdition? edition);
    public static IProEdition Create(IntPtr hwnd, IRegistryStore reg)
    {
        IProEdition? e = null;
        CreateProEdition(hwnd, reg, ref e);
        return e ?? new FreeEdition();
    }
}
