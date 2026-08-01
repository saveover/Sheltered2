using Microsoft.UI.Windowing;
using Microsoft.UI;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
using SaveOver.Sheltered2.Helpers;
using SaveOver.Sheltered2.Pages;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SaveOver.Sheltered2;

/// <summary>
/// Centralizes shell state that must remain coherent across cached pages: navigation selection,
/// title-bar geometry, editor availability, and the load/save busy lock.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const double MinimumWindowWidth = 640;
    private const double MinimumWindowHeight = 500;

    private readonly ILogger<MainWindow> logger = App.LoggerFactory.CreateLogger<MainWindow>();
    private bool isWorkspaceBusy;

    /// <summary>
    /// Keeps string tags in XAML as the single navigation contract instead of duplicating a
    /// switch in selection handling and another reverse mapping after frame navigation.
    /// </summary>
    private static readonly Dictionary<string, Type> PageMap = new()
    {
        ["Home"] = typeof(HomePage),
        ["Characters"] = typeof(CharactersPage),
        ["Pets"] = typeof(PetsPage),
        ["Inventory"] = typeof(InventoryPage),
        ["Crafting"] = typeof(CraftingPage),
        ["Factions"] = typeof(FactionsPage),
        ["Donate"] = typeof(DonatePage),
        [SettingsTag] = typeof(SettingsPage)
    };

    /// <summary>NavigationView creates SettingsItem itself, so it cannot share the XAML tag map.</summary>
    private const string SettingsTag = "Settings";

    /// <summary>
    /// Tags whose pages are always reachable, even before a save file is loaded.
    /// Every other page is a data editor and stays disabled until data is available.
    /// </summary>
    private static readonly HashSet<string> AlwaysEnabledTags = ["Home", "Donate"];

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Tall gives the caption buttons the full 48px strip rather than the 32px default, which is
        // what lets the icon, title and pane toggle sit level with them.
        AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

        // Native caption buttons sit outside the XAML theme inheritance tree. Follow the root's
        // effective theme explicitly, including live Windows theme changes while using Default.
        if (Content is FrameworkElement rootElement)
        {
            rootElement.ActualThemeChanged += (_, _) => UpdateCaptionButtonTheme(rootElement.ActualTheme);
            UpdateCaptionButtonTheme(rootElement.ActualTheme);
        }

        InitializeNavigation();

        // Editor pages stay locked until a save file has been loaded and decrypted.
        App.CurrentSaveData.SaveDataChanged += OnSaveDataChanged;
        UpdateNavigationEnabledState();

        // The TitleBar control sizes to its content, which here is just an icon and a caption -
        // about 32px. The caption buttons are 48 under Tall, so left alone the strip ends up
        // shorter than the buttons drawn over it and the content starts underneath them.
        AppTitleBar.Loaded += OnAppTitleBarLoaded;
    }

    private void OnAppTitleBarLoaded(object sender, RoutedEventArgs e)
    {
        AppTitleBar.Loaded -= OnAppTitleBarLoaded;

        if (AppTitleBar.XamlRoot is { } xamlRoot)
        {
            xamlRoot.Changed += OnXamlRootChanged;
        }

        UpdateWindowMetrics();
    }

    private void OnXamlRootChanged(XamlRoot sender, XamlRootChangedEventArgs args) =>
        UpdateWindowMetrics();

    private void UpdateWindowMetrics()
    {
        MatchWindowTitleBarHeight();
        ApplyMinimumWindowSize();
    }

    /// <summary>
    /// Keeps the resizable window large enough for the navigation shell and the pages' compact
    /// layouts. Presenter limits use physical pixels, so the effective-pixel minimum follows DPI.
    /// </summary>
    private void ApplyMinimumWindowSize()
    {
        if (Content is not FrameworkElement { XamlRoot: { } xamlRoot } ||
            AppWindow.Presenter is not OverlappedPresenter presenter)
        {
            return;
        }

        double scale = xamlRoot.RasterizationScale > 0 ? xamlRoot.RasterizationScale : 1;
        presenter.PreferredMinimumWidth = (int)Math.Ceiling(MinimumWindowWidth * scale);
        presenter.PreferredMinimumHeight = (int)Math.Ceiling(MinimumWindowHeight * scale);
    }

    private void UpdateCaptionButtonTheme(ElementTheme theme)
    {
        bool isDark = theme == ElementTheme.Dark;
        Windows.UI.Color foreground = isDark ? Colors.White : Colors.Black;
        AppWindow.TitleBar.ButtonForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverForegroundColor = foreground;
        AppWindow.TitleBar.ButtonHoverBackgroundColor = isDark
            ? Windows.UI.Color.FromArgb(24, 255, 255, 255)
            : Windows.UI.Color.FromArgb(24, 0, 0, 0);
    }

    /// <summary>
    /// Grows the title bar strip to whatever height the window has reserved for it. Read from the
    /// window rather than hard-coded, so it follows <see cref="TitleBarHeightOption"/>; the value
    /// is in physical pixels, so it is scaled back into the DIPs layout works in.
    /// </summary>
    private void MatchWindowTitleBarHeight()
    {
        double scale = AppTitleBar.XamlRoot?.RasterizationScale ?? 1;

        AppTitleBar.Height = AppWindow.TitleBar.Height / (scale <= 0 ? 1 : scale);
    }

    private void OnSaveDataChanged(object? sender, EventArgs e) => UpdateNavigationEnabledState();

    /// <summary>
    /// Moves the navigation items to the top of the window or back down the left edge.
    /// </summary>
    /// <remarks>
    /// Left is <see cref="NavigationViewPaneDisplayMode.Auto"/> rather than
    /// <see cref="NavigationViewPaneDisplayMode.Left"/>, so a narrow window still collapses the
    /// pane to icons and then to the flyout. Top has no pane at all, so the title bar's toggle
    /// button goes with it - it would otherwise sit there doing nothing.
    /// </remarks>
    internal void ApplyNavigationStyle(bool isTopStyle)
    {
        NavigationViewControl.PaneDisplayMode = isTopStyle
            ? NavigationViewPaneDisplayMode.Top
            : NavigationViewPaneDisplayMode.Auto;

        AppTitleBar.IsPaneToggleButtonVisible = !isTopStyle;
    }

    /// <summary>
    /// Prevents navigation while a background load or save operation is reading the shared model.
    /// </summary>
    internal void SetWorkspaceBusy(bool isBusy)
    {
        isWorkspaceBusy = isBusy;
        NavigationViewControl.IsEnabled = !isBusy;
    }

    private void OnTitleBarPaneToggleRequested(TitleBar sender, object args)
    {
        if (!isWorkspaceBusy)
        {
            NavigationViewControl.IsPaneOpen = !NavigationViewControl.IsPaneOpen;
        }
    }

    private void OnTitleBarBackRequested(TitleBar sender, object args)
    {
        if (!isWorkspaceBusy && RootFrame.CanGoBack)
        {
            RootFrame.GoBack();
        }
    }

    /// <summary>
    /// Keeps data editors unreachable without a model; disabling the navigation entry avoids
    /// forcing every editor page to invent a separate pre-load interaction state.
    /// </summary>
    private void UpdateNavigationEnabledState()
    {
        bool isLoaded = App.CurrentSaveData.IsLoaded;

        foreach (NavigationViewItem item in NavigationViewControl.MenuItems
            .Concat(NavigationViewControl.FooterMenuItems)
            .OfType<NavigationViewItem>())
        {
            if (item.Tag is string tag && !AlwaysEnabledTags.Contains(tag))
            {
                item.IsEnabled = isLoaded;
            }
        }
    }

    /// <summary>
    /// Routes calls from page content through NavigationView selection so programmatic and user
    /// navigation follow the same transition and selection path.
    /// </summary>
    public void NavigateToPageByTag(string pageTag)
    {
        NavigationViewItem? targetItem = NavigationViewControl.MenuItems
            .Concat(NavigationViewControl.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag?.ToString() == pageTag);

        if (targetItem != null)
        {
            NavigationViewControl.SelectedItem = targetItem;
        }
    }

    /// <summary>Seeds selection explicitly because the frame has no initial back-stack entry.</summary>
    private void InitializeNavigation()
    {
        // Navigated / NavigationFailed are wired in MainWindow.xaml; subscribing here
        // too would fire them twice per navigation.
        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().First();
        NavigateToPage("Home");
    }

    private void OnNavigationViewBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (!isWorkspaceBusy && RootFrame.CanGoBack)
        {
            RootFrame.GoBack();
        }
    }

    private void OnNavigationViewSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        // The settings entry is built into NavigationView and carries no Tag of its own.
        if (args.IsSettingsSelected)
        {
            NavigateToPage(SettingsTag);
        }
        else if (args.SelectedItem is NavigationViewItem { Tag: string pageTag })
        {
            NavigateToPage(pageTag);
        }
    }

    /// <summary>
    /// Reconciles shell selection after frame-driven navigation, including back navigation that
    /// bypasses NavigationView.SelectionChanged.
    /// </summary>
    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        AppTitleBar.IsBackButtonVisible = RootFrame.CanGoBack;

        // A cached page misses any theme switch that happened while it was off the visual tree, so
        // its InfoBars come back styled for the old theme. Once it is attached and templated, put
        // them right. The handler removes itself, so re-visiting a page doesn't stack handlers.
        if (e.Content is FrameworkElement arriving)
        {
            void RefreshInfoBars(object sender, RoutedEventArgs args)
            {
                arriving.Loaded -= RefreshInfoBars;
                ThemeHelper.RefreshInfoBars(arriving);
            }

            arriving.Loaded += RefreshInfoBars;
        }

        if (e.SourcePageType == typeof(SettingsPage))
        {
            NavigationViewControl.SelectedItem = NavigationViewControl.SettingsItem;
            return;
        }

        string tag = PageMap.FirstOrDefault(p => p.Value == e.SourcePageType).Key;

        NavigationViewItem? selectedItem = NavigationViewControl.MenuItems
            .Concat(NavigationViewControl.FooterMenuItems)
            .OfType<NavigationViewItem>()
            .FirstOrDefault(item => item.Tag as string == tag);

        if (selectedItem != null)
        {
            NavigationViewControl.SelectedItem = selectedItem;
        }
    }

    /// <summary>
    /// Fails fast after logging because leaving the shell selected on a page the frame could not
    /// construct produces a misleading, partially usable editor.
    /// </summary>
    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        logger.LogError(e.Exception, "Navigation failed to {PageType}.", e.SourcePageType.FullName);
        throw new InvalidOperationException($"Failed to load page {e.SourcePageType.FullName}.", e.Exception);
    }

    /// <summary>Avoids duplicate frame entries when selection is re-applied during synchronization.</summary>
    private void NavigateToPage(string pageTag)
    {
        if (PageMap.TryGetValue(pageTag, out Type? pageType) && RootFrame.CurrentSourcePageType != pageType)
        {
            _ = RootFrame.Navigate(pageType, null, new EntranceNavigationTransitionInfo());
        }
    }
}
