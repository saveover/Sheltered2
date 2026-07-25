// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using System;
using System.Collections.Generic;
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
    /// <summary>
    /// Accessible name and tooltip for the row's copy button. <see cref="DonatePage"/> strips
    /// the leading "Copy " off it to build the screen-reader confirmation.
    /// </summary>
    public string CopyLabel => $"Copy {Name} address";
}

/// <summary>
/// Lists the ways to support the project: donation platforms and crypto addresses.
/// </summary>
public sealed partial class DonatePage : Page
{
    /// <summary>Wallets bound to the Cryptocurrency card's repeater, in display order.</summary>
    public IReadOnlyList<CryptoWallet> Wallets { get; } =
    [
        new("Bitcoin", "bc1qqf3sdgc3l2hqmx0uw0xgul9cmnuanekmwk3ad3", Logo("bitcoin")),
        new("Ethereum", "0x895A4ce67b3F1641A441f88db9Ac5201205720C7", Logo("ethereum")),
        new("Cardano", "addr1qxpqzlfvg3zsywycy9aztuydr4skr78g7krffkl55cjpvrryq72xda08ngqwt65y7wrq8hw50s2hvzynp8aw2m737mzssektzj", Logo("cardano")),
        new("Solana", "8KomFrmvShJ5oCNbwZZXmz4K7ahzuLGURNJ8Wo8tEwzP", Logo("solana")),
        new("Litecoin", "ltc1q7amegshwzavg7vgqvd7nhx4u4xl3sw70j24chn", Logo("litecoin")),
    ];

    private static SvgImageSource Logo(string coin) => new(new Uri($"ms-appx:///Assets/Donation/{coin}.svg"));

    public DonatePage() => InitializeComponent();

    /// <summary>
    /// Copies the wallet address carried in the clicked button's Tag to the clipboard,
    /// announces the result via the live-region feedback text, and cross-fades the button's
    /// copy icon to a green checkmark and back (matching the Home page's copy feedback).
    /// </summary>
    private void CopyAddressButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string address } button)
        {
            return;
        }

        try
        {
            DataPackage dataPackage = new();
            dataPackage.SetText(address);
            Clipboard.SetContent(dataPackage);

            // The automation name is e.g. "Copy Bitcoin address"; reuse it for the feedback
            // line, which is a polite live region so screen readers announce the copy.
            string what = AutomationProperties.GetName(button).Replace("Copy ", string.Empty);
            SetFeedback($"{what} copied to clipboard.", "SystemFillColorSuccessBrush");

            ShowCopySuccessFeedback(button);
        }
        catch (Exception ex)
        {
            SetFeedback(
                "Could not copy the address. Please select and copy it manually.",
                "SystemFillColorCriticalBrush");
            System.Diagnostics.Debug.WriteLine($"Copy address error: {ex}");
        }
    }

    /// <summary>
    /// Writes the live-region feedback line and colours it for the outcome, so a failure
    /// doesn't get reported in the success green.
    /// </summary>
    private void SetFeedback(string message, string brushKey)
    {
        CopyFeedbackTextBlock.Text = message;

        if (Application.Current.Resources.TryGetValue(brushKey, out object? resource) && resource is Brush brush)
        {
            CopyFeedbackTextBlock.Foreground = brush;
        }
    }

    /// <summary>
    /// Cross-fades the clicked button's copy icon to the checkmark and back after a short
    /// delay. The button's content is a Grid holding the copy icon then the check icon; the
    /// two overlap and are driven by opacity and scale, matching HomePage's storyboards.
    /// </summary>
    private static void ShowCopySuccessFeedback(Button button)
    {
        if (button.Content is not Panel panel
            || panel.Children.Count < 2
            || panel.Children[0] is not FrameworkElement copyIcon
            || panel.Children[1] is not FrameworkElement checkIcon)
        {
            return;
        }

        CrossFade(copyIcon, checkIcon);

        // Cross-fade back to the copy icon after 2 seconds.
        DispatcherTimer timer = new() { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (s, args) =>
        {
            CrossFade(checkIcon, copyIcon);
            timer.Stop();
        };
        timer.Start();
    }

    // Property paths that walk from the icon to its centred CompositeTransform's scale.
    private const string ScaleXPath = "(UIElement.RenderTransform).(CompositeTransform.ScaleX)";
    private const string ScaleYPath = "(UIElement.RenderTransform).(CompositeTransform.ScaleY)";

    /// <summary>Fades and scales <paramref name="fadeOut"/> out while bringing <paramref name="fadeIn"/> in.</summary>
    private static void CrossFade(FrameworkElement fadeOut, FrameworkElement fadeIn)
    {
        EnsureCenteredTransform(fadeOut);
        EnsureCenteredTransform(fadeIn);

        Storyboard storyboard = new();

        AddAnimation(storyboard, fadeOut, "Opacity", null, 0, 0, 150, EaseOut());
        AddAnimation(storyboard, fadeOut, ScaleXPath, null, 0.8, 0, 150, EaseOut());
        AddAnimation(storyboard, fadeOut, ScaleYPath, null, 0.8, 0, 150, EaseOut());

        AddAnimation(storyboard, fadeIn, "Opacity", 0, 1, 100, 200, Bounce());
        AddAnimation(storyboard, fadeIn, ScaleXPath, 0.6, 1, 100, 200, Bounce());
        AddAnimation(storyboard, fadeIn, ScaleYPath, 0.6, 1, 100, 200, Bounce());

        storyboard.Begin();
    }

    private static void AddAnimation(
        Storyboard storyboard,
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
            To = to,
            BeginTime = TimeSpan.FromMilliseconds(beginMs),
            Duration = new Duration(TimeSpan.FromMilliseconds(durationMs)),
            EasingFunction = easing,
        };

        if (from.HasValue)
        {
            animation.From = from.Value;
        }

        Storyboard.SetTarget(animation, target);
        Storyboard.SetTargetProperty(animation, property);
        storyboard.Children.Add(animation);
    }

    /// <summary>Ensures the element scales about its centre, returning its composite transform.</summary>
    private static CompositeTransform EnsureCenteredTransform(FrameworkElement element)
    {
        if (element.RenderTransform is not CompositeTransform transform)
        {
            transform = new CompositeTransform();
            element.RenderTransform = transform;
        }

        element.RenderTransformOrigin = new Point(0.5, 0.5);
        return transform;
    }

    private static CubicEase EaseOut() => new() { EasingMode = EasingMode.EaseOut };

    private static BackEase Bounce() => new() { EasingMode = EasingMode.EaseOut, Amplitude = 0.3 };
}
