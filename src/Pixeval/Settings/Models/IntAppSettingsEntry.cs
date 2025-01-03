using System;
using System.Linq.Expressions;
using Pixeval.AppManagement;
using Pixeval.Controls.Settings;

namespace Pixeval.Settings.Models;

public partial class IntAppSettingsEntry : SingleValueSettingsEntry<AppSettings, int>, IDoubleSettingsEntry
{
    public IntAppSettingsEntry(AppSettings appSettings, Expression<Func<AppSettings, int>> property)
        : base(appSettings, property)
    {
       ((IDoubleSettingsEntry)this).ValueChanged = value => ValueChanged?.Invoke((int)value);
    }

    public override DoubleSettingsCard Element => new() { Entry = this };

    public string? Placeholder { get; set; }

    public double Max { get; set; } = double.MaxValue;

    public double Min { get; set; } = double.MinValue;

    public double LargeChange { get; set; } = 10;

    public double SmallChange { get; set; } = 1;

    public IntAppSettingsEntry(
        AppSettings appSettings,
        WorkTypeEnum workType,
        Expression<Func<AppSettings, int>> property)
        : this(appSettings, property)
    {
        Header = SubHeader(workType);
        HeaderIcon = SubHeaderIcon(workType);
    }

    Action<double>? ISingleValueSettingsEntry<double>.ValueChanged { get; set; }

    double ISingleValueSettingsEntry<double>.Value { get => Value; set => Value = (int)value; }
}
