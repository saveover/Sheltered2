// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace SaveOver.Sheltered2.Converters;

/// <summary>
/// Normalizes at the binding boundary so user edits stay within the game's percentage domain
/// without rewriting unrelated raw XML values during parsing.
/// </summary>
public sealed partial class PercentageNumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is double number && double.IsFinite(number)
            ? Normalize(number)
            : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is double number && double.IsFinite(number)
            ? Normalize(number)
            : DependencyProperty.UnsetValue;

    private static double Normalize(double value) =>
        Math.Round(Math.Clamp(value, 0, 100), MidpointRounding.AwayFromZero);
}
