using Microsoft.UI.Windowing;
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
/// The main application window containing the primary navigation and content frame.
/// </summary>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Maps page tags to their corresponding page types for navigation.
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

    /// <summary>Tag for the settings entry NavigationView provides for us.</summary>
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

        InitializeNavigation();

        // Editor pages stay locked until a save file has been loaded and decrypted.
        App.CurrentSaveData.SaveDataChanged += OnSaveDataChanged;
        UpdateNavigationEnabledState();

        // The TitleBar control sizes to its content, which here is just an icon and a caption -
        // about 32px. The caption buttons are 48 under Tall, so left alone the strip ends up
        // shorter than the buttons drawn over it and the content starts underneath them.
        AppTitleBar.Loaded += (_, _) =>
        {
            if (AppTitleBar.XamlRoot is { } xamlRoot)
            {
                xamlRoot.Changed += (_, _) => MatchWindowTitleBarHeight();
            }

            MatchWindowTitleBarHeight();
        };
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

    private void OnTitleBarPaneToggleRequested(TitleBar sender, object args) =>
        NavigationViewControl.IsPaneOpen = !NavigationViewControl.IsPaneOpen;

    private void OnTitleBarBackRequested(TitleBar sender, object args)
    {
        if (RootFrame.CanGoBack)
        {
            RootFrame.GoBack();
        }
    }

    /// <summary>
    /// Enables or disables the editor navigation items based on whether a save is loaded.
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
    /// Navigates to a specific page by its tag and updates the navigation selection.
    /// </summary>
    /// <param name="pageTag">The tag of the page to navigate to.</param>
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

    /// <summary>
    /// Sets up initial navigation state and event handlers.
    /// </summary>
    private void InitializeNavigation()
    {
        // Navigated / NavigationFailed are wired in MainWindow.xaml; subscribing here
        // too would fire them twice per navigation.
        NavigationViewControl.SelectedItem = NavigationViewControl.MenuItems.OfType<NavigationViewItem>().First();
        NavigateToPage("Home");
    }

    /// <summary>
    /// Handles back navigation requests.
    /// </summary>
    private void OnNavigationViewBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (RootFrame.CanGoBack)
        {
            RootFrame.GoBack();
        }
    }

    /// <summary>
    /// Handles navigation view selection changes.
    /// </summary>
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
    /// Synchronizes the NavigationView selection with the current page.
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
    /// Handles navigation failures by logging and throwing an exception.
    /// </summary>
    private void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine($"Navigation failed to {e.SourcePageType.FullName}: {e.Exception}");
        throw new InvalidOperationException($"Failed to load page {e.SourcePageType.FullName}.", e.Exception);
    }

    /// <summary>
    /// Navigates to a page based on its tag.
    /// </summary>
    /// <param name="pageTag">The tag identifying the target page.</param>
    private void NavigateToPage(string pageTag)
    {
        if (PageMap.TryGetValue(pageTag, out Type? pageType) && RootFrame.CurrentSourcePageType != pageType)
        {
            _ = RootFrame.Navigate(pageType, null, new EntranceNavigationTransitionInfo());
        }
    }
}
