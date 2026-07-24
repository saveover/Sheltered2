using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Navigation;
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
        ["Donate"] = typeof(DonatePage)
    };

    /// <summary>
    /// Tags whose pages are always reachable, even before a save file is loaded.
    /// Every other page is a data editor and stays disabled until data is available.
    /// </summary>
    private static readonly HashSet<string> AlwaysEnabledTags = ["Home", "Donate"];

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        InitializeNavigation();

        // Editor pages stay locked until a save file has been loaded and decrypted.
        App.CurrentSaveData.SaveDataChanged += OnSaveDataChanged;
        UpdateNavigationEnabledState();
    }

    private void OnSaveDataChanged(object? sender, EventArgs e) => UpdateNavigationEnabledState();

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
        if (args.SelectedItem is NavigationViewItem { Tag: string pageTag })
        {
            NavigateToPage(pageTag);
        }
    }

    /// <summary>
    /// Synchronizes the NavigationView selection with the current page.
    /// </summary>
    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        NavigationViewControl.IsBackEnabled = RootFrame.CanGoBack;

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