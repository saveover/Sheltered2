// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace SaveOver.Sheltered2.Converters;

/// <summary>
/// Shows a saved percentage as a whole number and normalizes only values edited by the user.
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
