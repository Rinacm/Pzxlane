// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using Pixeval.CoreApi.Model;
using Pixeval.Util;

namespace Pixeval.Controls;

public abstract partial class WorkEntryViewModel<T> : ThumbnailEntryViewModel<T>, IWorkViewModel where T : class, IWorkEntry
{
    protected WorkEntryViewModel(T entry) : base(entry) => InitializeCommands();

    IWorkEntry IWorkViewModel.Entry => Entry;

    public int TotalBookmarks => Entry.TotalBookmarks;

    public int TotalView => Entry.TotalView;

    public bool IsBookmarked
    {
        get => Entry.IsBookmarked;
        set => Entry.IsBookmarked = value;
    }

    public Tag[] Tags => Entry.Tags;

    public string Title => Entry.Title;

    public string Caption => Entry.Caption;

    public UserInfo User => Entry.User;

    public DateTimeOffset PublishDate => Entry.CreateDate;

    public bool IsAiGenerated => Entry.AiType is 2;

    public bool IsXRestricted => Entry.XRestrict is not XRestrict.Ordinary;

    public bool IsPrivate => Entry.IsPrivate;

    public bool IsMuted => Entry.IsMuted;

    public BadgeMode XRestrictionCaption =>
        Entry.XRestrict switch
        {
            XRestrict.R18G => BadgeMode.R18G,
            _ => BadgeMode.R18
        };

    protected override string ThumbnailUrl => Entry.GetThumbnailUrl();
}
