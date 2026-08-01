using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using SaveOver.Sheltered2.Helpers;
using System;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Windows.ApplicationModel;

namespace SaveOver.Sheltered2;

/// <summary>
/// Owns process-lifetime services that must survive page caching and navigation. Keeping the
/// logger factory and save session here prevents pages from creating competing baselines or
/// diagnostic pipelines.
/// </summary>
public partial class App : Application
{
    private readonly ILogger<App> logger;

    /// <summary>Shared so every component writes through the same retention and privacy policy.</summary>
    internal static ILoggerFactory LoggerFactory { get; } = ApplicationLogging.CreateLoggerFactory();

    /// <summary>Exposed because desktop pickers and helpers require the owning window identity.</summary>
    internal static Window? StartupWindow { get; private set; }

    /// <summary>
    /// The shared save session. One instance for the app's lifetime, so any page can
    /// subscribe to its <see cref="SaveSession.SaveDataChanged"/> event.
    /// </summary>
    internal static SaveSession CurrentSaveData { get; } = new();

    /// <summary>Registers failure logging before XAML initialization can execute user code.</summary>
    public App()
    {
        logger = LoggerFactory.CreateLogger<App>();
        LogSessionStarted();
        logger.LogInformation("Application initialization started.");

        UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        InitializeComponent();
    }

    /// <summary>
    /// Creates the window before applying helpers that depend on its XAML root, but applies them
    /// before activation so the first rendered frame already has the stored appearance.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        logger.LogInformation("Application launch started.");
        StartupWindow = new MainWindow();

        // After the window exists, since the theme and the navigation style are applied to it -
        // and before it is shown, so the app never flashes the wrong theme or moves its own menu.
        ThemeHelper.Initialize();
        NavigationStyleHelper.Initialize();
        SoundHelper.Initialize();

        StartupWindow.Activate();
        logger.LogInformation("Main window activated.");
    }

    private void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs args) => logger.LogCritical(
            args.Exception,
            "Unhandled exception from {ExceptionSource}.",
            "Microsoft.UI.Xaml.Application.UnhandledException");

    private void OnCurrentDomainUnhandledException(object sender, System.UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
        {
            logger.LogCritical(
                exception,
                "Unhandled exception from AppDomain.CurrentDomain.UnhandledException. Terminating: {IsTerminating}.",
                args.IsTerminating);
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args) =>
        logger.LogError(args.Exception, "An unobserved task exception reached TaskScheduler.");

    private void OnProcessExit(object? sender, EventArgs args)
    {
        logger.LogInformation("Application session ended.");
        LoggerFactory.Dispose();
    }

    private void LogSessionStarted()
    {
        Assembly assembly = typeof(App).Assembly;
        string version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString()
            ?? "Unknown";

        logger.LogInformation(
            "Application session started. Version: {Version}; Deployment: {Deployment}; OS: {OperatingSystem}; " +
            "Framework: {Framework}; Process architecture: {ProcessArchitecture}.",
            version,
            GetDeploymentKind(),
            RuntimeInformation.OSDescription,
            RuntimeInformation.FrameworkDescription,
            RuntimeInformation.ProcessArchitecture);
    }

    private static string GetDeploymentKind()
    {
        try
        {
            return $"Packaged ({Package.Current.Id.Name})";
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            // Package.Current is the authoritative signal, but it throws rather than returning
            // null when this same binary is launched from an unpackaged build.
            return "Unpackaged";
        }
    }
}
