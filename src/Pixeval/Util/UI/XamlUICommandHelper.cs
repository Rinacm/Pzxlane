#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/XamlUICommandHelper.cs
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

using Windows.System;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Pixeval.Controls.MarkupExtensions;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI;

namespace Pixeval.Util.UI;

public static class XamlUiCommandHelper
{
    public static XamlUICommand GetCommand(this string label, FontIconSymbol icon) =>
        GetCommand(label, icon.GetFontIconSource());

    public static XamlUICommand GetCommand(this string label, FontIconSymbol icon, VirtualKey key) =>
        GetCommand(label, icon.GetFontIconSource(), key);

    public static XamlUICommand GetCommand(this string label, FontIconSymbol icon, VirtualKeyModifiers modifiers, VirtualKey key) =>
        GetCommand(label, icon.GetFontIconSource(), modifiers, key);

    public static XamlUICommand GetCommand(this string label, IconSource icon) =>
        new()
        {
            Label = label,
            Description = label,
            IconSource = icon
        };

    public static XamlUICommand GetCommand(this string label, IconSource icon, VirtualKey key) =>
        GetCommand(label, icon, VirtualKeyModifiers.None, key);

    public static XamlUICommand GetCommand(this string label, IconSource icon, VirtualKeyModifiers modifiers, VirtualKey key) =>
        new()
        {
            Label = label,
            IconSource = icon,
            KeyboardAccelerators = { new KeyboardAccelerator { Modifiers = modifiers, Key = key } }
        };

    public static void GetBookmarkCommand(this XamlUICommand command, bool isBookmarked)
    {
        command.Label = command.Description = isBookmarked ? MiscResources.RemoveBookmark : MiscResources.AddBookmark;
        command.IconSource = isBookmarked
            ? FontIconSymbol.HeartFillEB52.GetFontIconSource(foregroundBrush: new SolidColorBrush(Colors.Crimson))
            : FontIconSymbol.HeartEB51.GetFontIconSource();
    }

    public static void GetFollowCommand(this XamlUICommand command, bool isFollowed)
    {
        command.Label = command.Description = isFollowed ? MiscResources.Unfollow : MiscResources.Follow;
        command.IconSource = isFollowed
            ? FontIconSymbol.ContactSolidEA8C.GetFontIconSource(foregroundBrush: new SolidColorBrush(Colors.Crimson))
            : FontIconSymbol.ContactE77B.GetFontIconSource();
    }

    public static XamlUICommand GetNewFollowCommand(bool isFollowed)
    {
        return (isFollowed ? MiscResources.Unfollow : MiscResources.Follow)
            .GetCommand(isFollowed
                ? FontIconSymbol.ContactSolidEA8C.GetFontIconSource(foregroundBrush: new SolidColorBrush(Colors.Crimson))
                : FontIconSymbol.ContactE77B.GetFontIconSource());
    }

    public static XamlUICommand GetNewFollowPrivatelyCommand()
    {
        return MiscResources.FollowPrivately.GetCommand(FontIconSymbol.FavoriteStarE734);
    }

    public static void GetPlayCommand(this XamlUICommand command, bool isPlaying)
    {
        command.Label = command.Description = isPlaying ? MiscResources.Pause : MiscResources.Play;
        command.IconSource = (isPlaying
            ? FontIconSymbol.StopE71A
            : FontIconSymbol.Play36EE4A).GetFontIconSource();
    }

    public static void GetResolutionCommand(this XamlUICommand command, bool isFit)
    {
        command.Label = command.Description = isFit ? MiscResources.RestoreOriginalResolution : MiscResources.UniformToFillResolution;
        command.IconSource = (isFit
            ? FontIconSymbol.WebcamE8B8
            : FontIconSymbol.FitPageE9A6).GetFontIconSource();
    }

    public static void GetFullScreenCommand(this XamlUICommand command, bool isFullScreen)
    {
        command.Label = command.Description = isFullScreen ? MiscResources.BackToWindow : MiscResources.FullScreen;
        command.IconSource = (isFullScreen
            ? FontIconSymbol.BackToWindowE73F
            : FontIconSymbol.FullScreenE740).GetFontIconSource();
    }
}
