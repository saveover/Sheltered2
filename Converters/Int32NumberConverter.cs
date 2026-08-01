// SPDX-License-Identifier: GPL-3.0-or-later
// Copyright (C) 2026 SaveOver

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using System;

namespace SaveOver.Sheltered2.Converters;

/// <summary>
/// Keeps integer save fields integer-backed even though <see cref="Microsoft.UI.Xaml.Controls.NumberBox"/>
/// exposes doubles; invalid transient input is rejected instead of being written as zero.
/// </summary>
public sealed partial class Int32NumberConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) =>
        value is int integer ? (double)integer : 0d;

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        value is double number && double.IsFinite(number)
            ? int.CreateSaturating(number)
            : DependencyProperty.UnsetValue;
}
