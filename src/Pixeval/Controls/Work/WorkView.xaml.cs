// Copyright (c) Pixeval.
// Licensed under the GPL v3 License.

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Windows.Foundation;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Pixeval.Pages.NovelViewer;
using Pixeval.Pages.IllustrationViewer;
using WinUI3Utilities;
using Pixeval.CoreApi.Model;
using Pixeval.CoreApi.Engine;
using Pixeval.Options;
using Microsoft.UI.Xaml.Media.Animation;
using Pixeval.CoreApi.Global.Enum;
using Pixeval.Util.UI;
using Pixeval.Pages.IllustratorViewer;

namespace Pixeval.Controls;

/// <summary>
/// todo
/// </summary>
[ObservableObject]
public sealed partial class WorkView : IEntryView<ISortableEntryViewViewModel>, IStructuralDisposalCompleter
{
    public const double LandscapeHeight = 180;
    public const double PortraitHeight = 250;

    public double DesiredHeight => ThumbnailDirection switch
    {
        ThumbnailDirection.Landscape => LandscapeHeight,
        ThumbnailDirection.Portrait => PortraitHeight,
        _ => ThrowHelper.ArgumentOutOfRange<ThumbnailDirection, double>(ThumbnailDirection)
    };

    public double DesiredWidth => ThumbnailDirection switch
    {
        ThumbnailDirection.Landscape => PortraitHeight,
        ThumbnailDirection.Portrait => LandscapeHeight,
        _ => ThrowHelper.ArgumentOutOfRange<ThumbnailDirection, double>(ThumbnailDirection)
    };

    public ItemsViewLayoutType LayoutType { get; set; }

    public ThumbnailDirection ThumbnailDirection { get; set; }

    public WorkView() => InitializeComponent();

    public event TypedEventHandler<WorkView, ISortableEntryViewViewModel>? ViewModelChanged;

    public AdvancedItemsView AdvancedItemsView => ItemsView;

    public ScrollView ScrollView => AdvancedItemsView.ScrollView;

    /// <summary>
    /// 在调用<see cref="ResetEngine"/>前为<see langword="null"/>
    /// </summary>
    [ObservableProperty]
    public partial ISortableEntryViewViewModel ViewModel { get; set; } = null!;

    [ObservableProperty]
    public partial SimpleWorkType Type { get; set; }

    private async void WorkItem_OnViewModelChanged(FrameworkElement sender, IWorkViewModel viewModel)
    {
        if (ViewModel == null!)
            return;
        if (await viewModel.TryLoadThumbnailAsync(ViewModel))
            if (sender.IsFullyOrPartiallyVisible(this))
                sender.GetResource<Storyboard>("ThumbnailStoryboard").Begin();
            else
                sender.Opacity = 1;
    }

    private void ItemsView_OnItemInvoked(ItemsView sender, ItemsViewItemInvokedEventArgs e)
    {
        switch (e.InvokedItem, ViewModel)
        {
            case (NovelItemViewModel viewModel, NovelViewViewModel viewViewModel):
                this.CreateNovelPage(viewModel, viewViewModel);
                break;
            case (IllustrationItemViewModel viewModel, IllustrationViewViewModel viewViewModel):
                this.CreateIllustrationPage(viewModel, viewViewModel);
                break;
        }
    }

    private void NovelItem_OnOpenNovelRequested(NovelItem sender, NovelItemViewModel viewModel)
    {
        if (ViewModel is NovelViewViewModel viewViewModel)
            this.CreateNovelPage(viewModel, viewViewModel);
    }

    private void WorkView_OnSelectionChanged(ItemsView sender, ItemsViewSelectionChangedEventArgs args)
    {
        if (ViewModel == null!)
            return;
        if (sender.SelectedItems is not { Count: > 0 })
        {
            ViewModel.SelectedEntries = ViewModel switch
            {
                NovelViewViewModel => (NovelItemViewModel[])[],
                IllustrationViewViewModel => (IllustrationItemViewModel[])[],
                _ => ViewModel.SelectedEntries
            };
            return;
        }

        ViewModel.SelectedEntries = ViewModel switch
        {
            NovelViewViewModel => sender.SelectedItems.Cast<NovelItemViewModel>().ToArray(),
            IllustrationViewViewModel => sender.SelectedItems.Cast<IllustrationItemViewModel>().ToArray(),
            _ => ViewModel.SelectedEntries
        };
    }

    [MemberNotNull(nameof(ViewModel))]
    public void ResetEngine(IFetchEngine<IWorkEntry> newEngine, int itemsPerPage = 20, int itemLimit = -1)
    {
        var type = newEngine.GetType().GetInterfaces()[0].GenericTypeArguments.FirstOrDefault();
        switch (ViewModel)
        {
            case NovelViewViewModel when type == typeof(Novel):
            case IllustrationViewViewModel when type == typeof(Illustration):
                ViewModel.ResetEngine(newEngine, itemsPerPage, itemLimit);
                break;
            default:
                if (type == typeof(Illustration))
                {
                    Type = SimpleWorkType.IllustAndManga;
                    ViewModel?.Dispose();
                    ViewModel = null!;
                    AdvancedItemsView.MinItemWidth = DesiredWidth;
                    AdvancedItemsView.MinItemHeight = DesiredHeight;
                    AdvancedItemsView.LayoutType = LayoutType;
                    AdvancedItemsView.ItemTemplate = this.GetResource<DataTemplate>("IllustrationItemDataTemplate");
                    ViewModel = new IllustrationViewViewModel();
                    OnPropertyChanged(nameof(ViewModel));
                    ViewModel.ResetEngine(newEngine, itemsPerPage, itemLimit);
                    ViewModelChanged?.Invoke(this, ViewModel);
                    AdvancedItemsView.ItemsSource = ViewModel.View;
                }
                else if (type == typeof(Novel))
                {
                    Type = SimpleWorkType.Novel;
                    ViewModel?.Dispose();
                    ViewModel = null!;
                    AdvancedItemsView.MinItemWidth = 350;
                    AdvancedItemsView.MinItemHeight = 200;
                    AdvancedItemsView.LayoutType = ItemsViewLayoutType.Grid;
                    AdvancedItemsView.ItemTemplate = this.GetResource<DataTemplate>("NovelItemDataTemplate");
                    ViewModel = new NovelViewViewModel();
                    OnPropertyChanged(nameof(ViewModel));
                    ViewModel.ResetEngine(newEngine, itemsPerPage, itemLimit);
                    ViewModelChanged?.Invoke(this, ViewModel);
                    AdvancedItemsView.ItemsSource = ViewModel.View;
                }
                else
                    ThrowHelper.ArgumentOutOfRange(ViewModel);
                break;
        }
    }

    private TeachingTip WorkItem_OnRequestTeachingTip() => EntryView.QrCodeTeachingTip;

    private (ThumbnailDirection ThumbnailDirection, double DesiredHeight) IllustrationItem_OnRequiredParam() => (ThumbnailDirection, DesiredHeight);

    private void AddToBookmarkTeachingTip_OnCloseButtonClick(TeachingTip sender, object e)
    {
        sender.GetTag<IWorkViewModel>().AddToBookmarkCommand.Execute((BookmarkTagSelector.SelectedTags, BookmarkTagSelector.IsPrivate, null as object));

        this.SuccessGrowl(EntryViewResources.AddedToBookmark);
    }

    private void WorkItem_OnRequestAddToBookmark(FrameworkElement sender, IWorkViewModel e)
    {
        AddToBookmarkTeachingTip.Tag = e;
        AddToBookmarkTeachingTip.IsOpen = true;
    }

    public async void WorkItem_OnRequestOpenUserInfoPage(FrameworkElement sender, IWorkViewModel e)
    {
        await this.CreateIllustratorPageAsync(e.User.Id);
    }

    public void CompleteDisposal()
    {
        if (ViewModel == null!)
            return;
        var viewModel = ViewModel;
        ViewModel = null!;
        foreach (var vm in viewModel.Source)
            vm.UnloadThumbnail(viewModel);
        viewModel.Dispose();
    }

    public List<Action> ChildrenCompletes { get; } = [];

    public bool CompleterRegistered { get; set; }

    public bool CompleterDisposed { get; set; }
}
