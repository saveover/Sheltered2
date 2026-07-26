// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using SaveOver.Sheltered2.Helpers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;

namespace SaveOver.Sheltered2.Pages;

/// <summary>
/// One membership tier in the donate page's Memberships grid.
/// </summary>
/// <param name="Name">Tier name shown at the top of the card, e.g. "Supporter".</param>
/// <param name="Price">Price as displayed, e.g. "$5 / month".</param>
/// <param name="Summary">One line on what the tier pays for.</param>
/// <param name="Perks">Rewards listed under the divider, in display order.</param>
public sealed record DonationTier(string Name, string Price, string Summary, IReadOnlyList<string> Perks);

/// <summary>
/// One membership platform in the donate page's Join card.
/// </summary>
/// <param name="Name">Platform name, shown as the plate's tooltip.</param>
/// <param name="Url">Where the plate links to.</param>
/// <param name="Logo">Brand artwork from Assets/Donation.</param>
public sealed record DonationPlatform(string Name, Uri Url, ImageSource Logo)
{
    /// <summary>Accessible name for the plate, which is otherwise only an image.</summary>
    public string LinkLabel => $"Support on {Name} (opens in your browser)";
}

/// <summary>
/// One off-app link in the donate page's "Other ways to help" card.
/// </summary>
/// <param name="Label">Link text.</param>
/// <param name="Url">Where the link goes.</param>
/// <param name="Destination">Where the link leads, spelled out for screen readers.</param>
public sealed record HelpLink(string Label, Uri Url, string Destination)
{
    /// <summary>Accessible name: the destination, plus a warning that it leaves the app.</summary>
    public string LinkLabel => $"{Destination} (opens in your browser)";
}

/// <summary>
/// One wallet in the donate page's Cryptocurrency card.
/// </summary>
/// <param name="Name">Coin name shown above the address, e.g. "Bitcoin".</param>
/// <param name="Address">Receiving address, copied to the clipboard verbatim.</param>
/// <param name="Icon">Coin logo from Assets/Donation.</param>
public sealed record CryptoWallet(string Name, string Address, ImageSource Icon)
{
    /// <summary>Accessible name and tooltip for the row's copy button.</summary>
    public string CopyLabel => $"Copy {Name} address";
}

/// <summary>
/// Lists the ways to support the project: membership tiers, donation platforms, and crypto
/// addresses. Every repeated section on the page is one template over the content below.
/// </summary>
public sealed partial class DonatePage : Page
{
    /// <summary>The three tier cards, in ascending order. Bound one per card rather than as a
    /// list because the adaptive states below have to address each card by name.</summary>
    public DonationTier Supporter { get; } = new(
        "Supporter",
        "$5 / month",
        "Keeps the editors free for everyone. This funds the time that goes into building them and gets you a few things back!",
        [
            "Early access to new editors before they go public",
            "Your name or handle in the credits",
            "Exclusive access to private channels on our Discord",
        ]);

    /// <inheritdoc cref="Supporter"/>
    public DonationTier Patron { get; } = new(
        "Patron",
        "$10 / month",
        "Members receive all Supporter perks, gain access to behind-the-scenes notes during editor development, and may propose and vote on which game gets an editor next.",
        [
            "Everything from previous tier",
            "Propose and vote on which game gets an editor next",
            "Behind-the-scenes posts while an editor is being built",
        ]);

    /// <inheritdoc cref="Supporter"/>
    public DonationTier Founder { get; } = new(
        "Founder",
        "$25 / month",
        "Members receive all Patron perks, are acknowledged in a dedicated section of the credits, and may provide input on features for both future and previous editors.",
        [
            "Everything from previous tier",
            "Get your name or handle listed in a special credits section",
            "Share your ideas for new and existing editor features",
        ]);

    /// <summary>Platforms bound to the Join card's repeater, in display order.</summary>
    public IReadOnlyList<DonationPlatform> Platforms { get; } =
    [
        new("Buy Me a Coffee", new("https://buymeacoffee.com/saveover"), Logo("bmc")),
        new("Ko-fi", new("https://ko-fi.com/saveover"), Logo("kofi")),
        new("Patreon", new("https://www.patreon.com/cw/saveover"), Logo("patreon")),
    ];

    /// <summary>Wallets bound to the Cryptocurrency card's repeater, in display order.</summary>
    public IReadOnlyList<CryptoWallet> Wallets { get; } =
    [
        new("Bitcoin", "bc1qqf3sdgc3l2hqmx0uw0xgul9cmnuanekmwk3ad3", Logo("bitcoin")),
        new("Ethereum", "0x895A4ce67b3F1641A441f88db9Ac5201205720C7", Logo("ethereum")),
        new("Cardano", "addr1qxpqzlfvg3zsywycy9aztuydr4skr78g7krffkl55cjpvrryq72xda08ngqwt65y7wrq8hw50s2hvzynp8aw2m737mzssektzj", Logo("cardano")),
        new("Solana", "8KomFrmvShJ5oCNbwZZXmz4K7ahzuLGURNJ8Wo8tEwzP", Logo("solana")),
        new("Litecoin", "ltc1q7amegshwzavg7vgqvd7nhx4u4xl3sw70j24chn", Logo("litecoin")),
    ];

    /// <summary>Links bound to the "Other ways to help" card's repeater, in display order.</summary>
    public IReadOnlyList<HelpLink> HelpLinks { get; } =
    [
        new("Join the Discord", new("https://discord.gg/nzQSeGcta8"), "Join the SaveOver Discord"),
        new("Report an issue", new("https://github.com/saveover/Sheltered2/issues"), "Report an issue on GitHub"),
        new("Browse the code", new("https://github.com/saveover/Sheltered2"), "Browse the source on GitHub"),
    ];

    /// <summary>Drives the copy-to-checkmark swap. One per page, so copying a second wallet hands
    /// the first row's checkmark back rather than leaving two ticks on screen.</summary>
    private readonly CopyIconFeedback _copyFeedback = new();

    private static SvgImageSource Logo(string name) => new(new Uri($"ms-appx:///Assets/Donation/{name}.svg"));

    public DonatePage() => InitializeComponent();

    /// <summary>
    /// Copies the clicked row's address to the clipboard, cross-fades the button's copy icon to a
    /// checkmark, and announces the outcome to assistive technology.
    /// </summary>
    private void CopyAddressButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: CryptoWallet wallet } button)
        {
            return;
        }

        try
        {
            DataPackage package = new();
            package.SetText(wallet.Address);
            Clipboard.SetContent(package);
        }
        catch (Exception ex)
        {
            const string failure = "Could not copy the address. Please select and copy it manually.";

            CopyErrorInfoBar.Message = failure;
            CopyErrorInfoBar.IsOpen = true;
            Announce(failure);
            Debug.WriteLine($"Copy address error: {ex}");
            return;
        }

        CopyErrorInfoBar.IsOpen = false;
        _copyFeedback.Play(button);
        Announce($"{wallet.Name} address copied to clipboard.");
    }

    /// <summary>
    /// Speaks <paramref name="message"/> to a screen reader without putting anything on screen.
    /// </summary>
    /// <remarks>
    /// A notification event rather than a live region: a live region needs a visible element to
    /// hang off, which meant carrying a line of status text the sighted user never needed - the
    /// checkmark already tells them. MostRecent means a burst of copies announces only the last.
    /// </remarks>
    private void Announce(string message)
    {
        AutomationPeer? peer = FrameworkElementAutomationPeer.FromElement(this)
            ?? FrameworkElementAutomationPeer.CreatePeerForElement(this);

        peer?.RaiseNotificationEvent(
            AutomationNotificationKind.ActionCompleted,
            AutomationNotificationProcessing.MostRecent,
            message,
            "SaveOver.CopyAddress");
    }
}
