#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/IllustrationViewOptionSettingEntryItem.cs
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.
#endregion

using Pixeval.Options;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Pixeval.Controls.Setting.UI.Model;

public record IllustrationViewOptionSettingEntryItem : IStringRepresentableItem
{
    public IllustrationViewOptionSettingEntryItem(IllustrationViewOption item)
    {
        Item = item;
        StringRepresentation = item switch
        {
            IllustrationViewOption.Regular => MiscResources.IllustrationViewRegularLayout,
            IllustrationViewOption.RiverFlow => MiscResources.IllustrationViewJustifiedLayout,
            _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
        };
    }

    public object Item { get; }

    public string StringRepresentation { get; }

    public static IEnumerable<IStringRepresentableItem> AvailableItems { get; } = Enum.GetValues<IllustrationViewOption>().Select(i => new IllustrationViewOptionSettingEntryItem(i));
}
