// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
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
        "Keeps the editors free for everyone.",
        [
            "Early access to new editors before they go public",
            "Your name or handle in the credits",
            "Private channels on the Discord",
        ]);

    /// <inheritdoc cref="Supporter"/>
    public DonationTier Patron { get; } = new(
        "Patron",
        "$10 / month",
        "A say in what game gets an editor next.",
        [
            "Everything in Supporter",
            "Propose and vote on which game gets an editor next",
            "Behind-the-scenes notes while an editor is being built",
        ]);

    /// <inheritdoc cref="Supporter"/>
    public DonationTier Founder { get; } = new(
        "Founder",
        "$25 / month",
        "A say in what features an editor gets next.",
        [
            "Everything in Patron",
            "Influence the features shipped with an editor",
            "Your name in the Founders section, at the top of the credits",
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
    /// Copies the clicked row's address to the clipboard, announces it through the live-region
    /// feedback line, and cross-fades the button's copy icon to a checkmark and back.
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
            SetFeedback("Could not copy the address. Please select and copy it manually.", success: false);
            Debug.WriteLine($"Copy address error: {ex}");
            return;
        }

        SetFeedback($"{wallet.Name} address copied to clipboard.", success: true);
        _copyFeedback.Play(button);
    }

    /// <summary>
    /// Writes the live-region feedback line and colours it for the outcome, so a failure
    /// isn't reported in the success green.
    /// </summary>
    private void SetFeedback(string message, bool success)
    {
        CopyFeedbackTextBlock.Text = message;
        CopyFeedbackTextBlock.Foreground = (Brush)Resources[success ? "CopySuccessBrush" : "CopyErrorBrush"];
    }
}
