using Microsoft.UI.Xaml;
namespace VetTV.Launcher.App;
public partial class App : Application
{
    private Window? _window;
    public static bool IsShellSession { get; private set; }
    public App() => InitializeComponent();
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        IsShellSession = Environment.GetCommandLineArgs().Contains("--shell");
        _window = new MainWindow();
        _window.Activate();
    }
}
