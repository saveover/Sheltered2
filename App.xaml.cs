using Microsoft.UI.Xaml;
using SaveOver.Sheltered2.Helpers;

namespace SaveOver.Sheltered2;
/// <summary>
/// Provides application-specific behaviour to supplement the default Application class.
/// </summary>
public partial class App : Application
{
    /// <summary>
    /// Gets the initial window created for this app.
    /// </summary>
    internal static Window? StartupWindow { get; private set; }

    /// <summary>
    /// The shared save session. One instance for the app's lifetime, so any page can
    /// subscribe to its <see cref="SaveSession.SaveDataChanged"/> event.
    /// </summary>
    internal static SaveSession CurrentSaveData { get; } = new();

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App() => InitializeComponent();

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        StartupWindow = new MainWindow();

        // After the window exists, since the theme is applied to its root element - and before it
        // is shown, so the app never flashes the wrong theme.
        ThemeHelper.Initialize();

        StartupWindow.Activate();
    }
}