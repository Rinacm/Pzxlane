// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Pixeval.Controls.Settings;
using Pixeval.Extensions.Common.Settings;
using Pixeval.Settings;

namespace Pixeval.Extensions.Models;

public partial class ExtensionIntSettingsEntry(IIntSettingsExtension extension, int value, IPropertySet values)
    : ExtensionSettingsEntry<int>(extension, value, values), IDoubleSettingsEntry
{
    public override FrameworkElement Element => new DoubleSettingsCard { Entry = this };

    /// <remarks>
    /// 它和<see cref="SingleValueSettingsEntry{T1, T2}.Value"/>名称相同，所以<see cref="ObservableSettingsEntryBase.OnPropertyChanged"/>只需要触发其中一个就行
    /// </remarks>
    double ISingleValueSettingsEntry<double>.Value
    {
        get => Value;
        set => Value = (int)value;
    }

    public override void ValueSaving()
    {
        extension.OnValueChanged(Value);
        base.ValueSaving();
    }

    public string? Placeholder => extension.GetPlaceholder();

    public double Max => extension.GetMaxValue();

    public double Min => extension.GetMinValue();

    public double LargeChange => extension.GetLargeChange();

    public double SmallChange => extension.GetSmallChange();
}
