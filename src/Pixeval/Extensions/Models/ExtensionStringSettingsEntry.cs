// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using Windows.Foundation.Collections;
using Microsoft.UI.Xaml;
using Pixeval.Controls.Settings;
using Pixeval.Extensions.Common.Settings;
using Pixeval.Settings;

namespace Pixeval.Extensions.Models;

public partial class ExtensionStringSettingsEntry(IStringSettingsExtension extension, string value, IPropertySet values) : ExtensionSettingsEntry<string>(extension, value, values), IStringSettingsEntry
{
    public override FrameworkElement Element => new StringSettingsCard { Entry = this };

    public string? Placeholder => extension.GetPlaceholder();

    public override void ValueSaving()
    {
        extension.OnValueChanged(Value);
        base.ValueSaving();
    }
}
