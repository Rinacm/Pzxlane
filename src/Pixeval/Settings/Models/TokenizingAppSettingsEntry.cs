using System.Collections.ObjectModel;
using Pixeval.AppManagement;
using Pixeval.Controls.Settings;

namespace Pixeval.Settings.Models;

public partial class TokenizingAppSettingsEntry(
    AppSettings appSettings)
    : ObservableSettingsEntryBase("", "", default)
{
    public override TokenizingSettingsExpander Element => new() { Entry = this };

    public AppSettings Settings { get; } = appSettings;

    public override void ValueReset()
    {
        BlockedTags = [.. Settings.BlockedTags];
        OnPropertyChanged(nameof(BlockedTags));
    }

    public override void ValueSaving() => Settings.BlockedTags = [.. BlockedTags];

    public ObservableCollection<string> BlockedTags { get; set; } = [.. appSettings.BlockedTags];
}
