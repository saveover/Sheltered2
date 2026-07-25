// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;

namespace SaveOver.Sheltered2.Pages;

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
/// Lists the ways to support the project: donation platforms and crypto addresses.
/// </summary>
public sealed partial class DonatePage : Page
{
    /// <summary>How long the checkmark stays up before the copy icon fades back in.</summary>
    private const int HoldMs = 2000;

    private const int FadeOutMs = 150;
    private const int FadeInMs = 200;

    // Property paths that walk from an icon to its centred CompositeTransform's scale.
    private const string ScaleXPath = "(UIElement.RenderTransform).(CompositeTransform.ScaleX)";
    private const string ScaleYPath = "(UIElement.RenderTransform).(CompositeTransform.ScaleY)";

    private static readonly Point Center = new(0.5, 0.5);

    /// <summary>Wallets bound to the Cryptocurrency card's repeater, in display order.</summary>
    public IReadOnlyList<CryptoWallet> Wallets { get; } =
    [
        new("Bitcoin", "bc1qqf3sdgc3l2hqmx0uw0xgul9cmnuanekmwk3ad3", Logo("bitcoin")),
        new("Ethereum", "0x895A4ce67b3F1641A441f88db9Ac5201205720C7", Logo("ethereum")),
        new("Cardano", "addr1qxpqzlfvg3zsywycy9aztuydr4skr78g7krffkl55cjpvrryq72xda08ngqwt65y7wrq8hw50s2hvzynp8aw2m737mzssektzj", Logo("cardano")),
        new("Solana", "8KomFrmvShJ5oCNbwZZXmz4K7ahzuLGURNJ8Wo8tEwzP", Logo("solana")),
        new("Litecoin", "ltc1q7amegshwzavg7vgqvd7nhx4u4xl3sw70j24chn", Logo("litecoin")),
    ];

    /// <summary>Fades the checkmark back once <see cref="HoldMs"/> has elapsed. Reused across
    /// copies so a second copy can cancel the reset pending on the first.</summary>
    private readonly DispatcherTimer _resetTimer = new() { Interval = TimeSpan.FromMilliseconds(HoldMs) };

    /// <summary>The icons of the row currently showing a checkmark, if any.</summary>
    private (FrameworkElement Copy, FrameworkElement Check)? _shownRow;

    private static SvgImageSource Logo(string coin) => new(new Uri($"ms-appx:///Assets/Donation/{coin}.svg"));

    public DonatePage()
    {
        InitializeComponent();
        _resetTimer.Tick += (_, _) => RestoreShownRow();
    }

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

        // The icon swap is decoration. With the address already safely on the clipboard, a
        // failure here is not worth taking the app down for.
        try
        {
            PlayCopyFeedback(button);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Copy feedback animation error: {ex}");
        }
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

    /// <summary>
    /// Cross-fades the button's copy icon to the checkmark and starts the clock that fades it
    /// back. The button's content is a Grid holding the copy icon then the checkmark; the two
    /// overlap and are driven by opacity and scale, matching HomePage's storyboards.
    /// </summary>
    private void PlayCopyFeedback(Button button)
    {
        if (button.Content is not Panel { Children: [FrameworkElement copyIcon, FrameworkElement checkIcon, ..] })
        {
            return;
        }

        // A second copy before the first has reset would leave two rows showing a checkmark
        // and two resets racing, so settle the previous row before starting this one.
        RestoreShownRow();

        CrossFade(copyIcon, checkIcon);
        _shownRow = (copyIcon, checkIcon);
        _resetTimer.Start();
    }

    /// <summary>Fades the checkmark back to the copy icon on whichever row is showing one.</summary>
    private void RestoreShownRow()
    {
        _resetTimer.Stop();

        if (_shownRow is (FrameworkElement copyIcon, FrameworkElement checkIcon))
        {
            CrossFade(checkIcon, copyIcon);
            _shownRow = null;
        }
    }

    /// <summary>Fades and scales <paramref name="fadeOut"/> out while bringing <paramref name="fadeIn"/> in.</summary>
    private static void CrossFade(FrameworkElement fadeOut, FrameworkElement fadeIn)
    {
        CenterForScaling(fadeOut);
        CenterForScaling(fadeIn);

        CubicEase easeOut = new() { EasingMode = EasingMode.EaseOut };
        BackEase bounce = new() { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
        Storyboard storyboard = new();

        // One storyboard per direction: a storyboard must not animate the same property of the
        // same element twice, which rules out putting the return leg in here on a delay.
        // Leaving the outgoing From null starts it from wherever it currently sits, so an
        // interrupted fade picks up mid-flight instead of snapping.
        Add(fadeOut, "Opacity", null, 0, 0, FadeOutMs, easeOut);
        Add(fadeOut, ScaleXPath, null, 0.8, 0, FadeOutMs, easeOut);
        Add(fadeOut, ScaleYPath, null, 0.8, 0, FadeOutMs, easeOut);

        // The incoming icon overlaps the outgoing one by starting before it finishes.
        Add(fadeIn, "Opacity", 0, 1, 100, FadeInMs, bounce);
        Add(fadeIn, ScaleXPath, 0.6, 1, 100, FadeInMs, bounce);
        Add(fadeIn, ScaleYPath, 0.6, 1, 100, FadeInMs, bounce);

        storyboard.Begin();

        void Add(
            DependencyObject target,
            string property,
            double? from,
            double to,
            int beginMs,
            int durationMs,
            EasingFunctionBase easing)
        {
            DoubleAnimation animation = new()
            {
                From = from,
                To = to,
                BeginTime = TimeSpan.FromMilliseconds(beginMs),
                Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
                EasingFunction = easing,
            };

            Storyboard.SetTarget(animation, target);
            Storyboard.SetTargetProperty(animation, property);
            storyboard.Children.Add(animation);
        }
    }

    /// <summary>Gives the element a <see cref="CompositeTransform"/> that scales about its centre.</summary>
    private static void CenterForScaling(FrameworkElement element)
    {
        if (element.RenderTransform is not CompositeTransform)
        {
            element.RenderTransform = new CompositeTransform();
        }

        element.RenderTransformOrigin = Center;
    }
}
