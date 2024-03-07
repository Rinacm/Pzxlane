#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/IllustrationViewerPageViewModel.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Pixeval.AppManagement;
using Pixeval.Controls;
using Pixeval.Controls.MarkupExtensions;
using Pixeval.Controls.Windowing;
using Pixeval.CoreApi.Model;
using Pixeval.Util.IO;
using Pixeval.Util.UI;
using Pixeval.Utilities;
using Pixeval.Util.ComponentModels;

namespace Pixeval.Pages.IllustrationViewer;

public partial class IllustrationViewerPageViewModel : DetailedUiObservableObject, IDisposable
{
    [ObservableProperty]
    private bool _isFullScreen;

    // The reason why we don't put UserProfileImageSource into IllustrationViewModel
    // is because the whole array of Illustrations is just representing the same 
    // illustration's different manga pages, so all of them have the same illustrator
    // If the UserProfileImageSource is in IllustrationViewModel and the illustration
    // itself is a manga then all of IllustrationViewModel in Illustrations will
    // request the same user profile image which is pointless and will (inevitably) causing
    // the waste of system resource
    [ObservableProperty]
    private ImageSource? _userProfileImageSource;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="illustrationViewModels"></param>
    /// <param name="currentIllustrationIndex"></param>
    /// <param name="content"></param>
    public IllustrationViewerPageViewModel(IEnumerable<IllustrationItemViewModel> illustrationViewModels, int currentIllustrationIndex, FrameworkElement content) : base(content)
    {
        IllustrationsSource = illustrationViewModels.ToArray();
        IllustrationInfoTag.Parameter = this;
        CurrentIllustrationIndex = currentIllustrationIndex;

        InitializeCommands();
        FullScreenCommand.GetFullScreenCommand(false);
    }

    /// <summary>
    /// 当拥有DataProvider的时候调用这个构造函数，dispose的时候会自动dispose掉DataProvider
    /// </summary>
    /// <param name="viewModel"></param>
    /// <param name="currentIllustrationIndex"></param>
    /// <param name="content"></param>
    /// <remarks>
    /// illustrations should contain only one item if the illustration is a single
    /// otherwise it contains the entire manga data
    /// </remarks>
    public IllustrationViewerPageViewModel(IllustrationViewViewModel viewModel, int currentIllustrationIndex, FrameworkElement content) : base(content)
    {
        ViewModelSource = new IllustrationViewViewModel(viewModel);
        IllustrationInfoTag.Parameter = this;
        ViewModelSource.DataProvider.View.FilterChanged += (_, _) => CurrentIllustrationIndex = Illustrations.IndexOf(CurrentIllustration);
        CurrentIllustrationIndex = currentIllustrationIndex;

        InitializeCommands();
    }

    private IllustrationViewViewModel? ViewModelSource { get; }

    public IllustrationItemViewModel[]? IllustrationsSource { get; }

    public long IllustrationId => CurrentIllustration.Entry.Id;

    public UserInfo Illustrator => CurrentIllustration.Entry.User;

    public string IllustratorName => Illustrator.Name;

    public long IllustratorId => Illustrator.Id;

    public void Dispose()
    {
        foreach (var illustrationViewModel in Illustrations)
            illustrationViewModel.UnloadThumbnail(this);
        Pages = null!;
        (UserProfileImageSource as SoftwareBitmapSource)?.Dispose();
        ViewModelSource?.Dispose();
    }

    #region Tags for IllustrationInfoAndCommentsNavigationView

    public NavigationViewTag<IllustrationInfoPage, IllustrationViewerPageViewModel> IllustrationInfoTag { get; } = new(null!);

    public NavigationViewTag<CommentsPage, (IAsyncEnumerable<Comment?>, long IllustrationId)> CommentsTag { get; } = new(default);

    public NavigationViewTag<RelatedWorksPage, long> RelatedWorksTag { get; } = new(default);

    #endregion

    #region Current相关

    /// <summary>
    /// 插画列表
    /// </summary>
    public IList<IllustrationItemViewModel> Illustrations => ViewModelSource?.DataProvider.View ?? (IList<IllustrationItemViewModel>)IllustrationsSource!;

    /// <summary>
    /// 当前插画
    /// </summary>
    public IllustrationItemViewModel CurrentIllustration => Illustrations[CurrentIllustrationIndex];

    /// <summary>
    /// 当前插画的页面
    /// </summary>
    public IllustrationItemViewModel CurrentPage => Pages[CurrentPageIndex];

    /// <summary>
    /// 当前插画的索引
    /// </summary>
    public int CurrentIllustrationIndex
    {
        get => _currentIllustrationIndex;
        set
        {
            if (value is -1)
                return;
            if (value == _currentIllustrationIndex)
                return;

            var oldValue = _currentIllustrationIndex;
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            var oldTag = Pages?[CurrentPageIndex].Id ?? 0;

            _currentIllustrationIndex = value;
            // 这里可以触发总页数的更新
            Pages = CurrentIllustration.GetMangaIllustrationViewModels().ToArray();
            // 保证_pages里所有的IllustrationViewModel都是生成的，从而删除的时候一律DisposeForce

            RelatedWorksTag.Parameter = IllustrationId;
            // IllustrationInfoTag.Parameter = this;
            CommentsTag.Parameter = (App.AppViewModel.MakoClient.IllustrationComments(IllustrationId).Where(c => c is not null), IllustrationId);

            _ = LoadUserProfile();

            // 此处不要触发CurrentPageIndex的OnDetailedPropertyChanged，否则会导航两次
            _currentPageIndex = 0;
            OnButtonPropertiesChanged();
            // 用OnPropertyChanged不会触发导航，但可以让UI页码更新
            OnPropertyChanged(nameof(CurrentPageIndex));
            CurrentImage = new ImageViewerPageViewModel(CurrentPage, FrameworkElement);

            OnDetailedPropertyChanged(oldValue, value, oldTag, CurrentPage.Id);
            OnPropertyChanged(nameof(CurrentIllustration));
            return;

            async Task LoadUserProfile()
            {
                if (Illustrator is { ProfileImageUrls.Medium: { } profileImage })
                {
                    var result = await App.AppViewModel.MakoClient.DownloadSoftwareBitmapSourceAsync(profileImage);
                    UserProfileImageSource = result is Result<SoftwareBitmapSource>.Success { Value: var avatar }
                        ? avatar
                        : await AppInfo.GetPixivNoProfileImageAsync();
                }
            }
        }
    }

    /// <summary>
    /// 当前插画的页面索引
    /// </summary>
    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            if (value == _currentPageIndex)
                return;
            var oldValue = _currentPageIndex;
            _currentPageIndex = value;
            OnButtonPropertiesChanged();
            CurrentImage = new ImageViewerPageViewModel(CurrentPage, FrameworkElement);
            OnDetailedPropertyChanged(oldValue, value);
        }
    }

    private void OnButtonPropertiesChanged()
    {
        OnPropertyChanged(nameof(NextButtonText));
        OnPropertyChanged(nameof(PrevButtonText));
    }

    public int PageCount => Pages.Length;

    /// <inheritdoc cref="CurrentIllustrationIndex"/>
    private int _currentIllustrationIndex = -1;

    /// <inheritdoc cref="CurrentPageIndex"/>
    private int _currentPageIndex = -1;

    /// <summary>
    /// 一个插画所有的页面
    /// </summary>
    public IllustrationItemViewModel[] Pages
    {
        get => _pages;
        set
        {
            if (_pages == value)
                return;
            _pages?.ForEach(i => i.Dispose());
            _pages = value;
            if (_pages != null!)
                OnPropertyChanged(nameof(PageCount));
        }
    }

    private IllustrationItemViewModel[] _pages = null!;

    /// <summary>
    /// 当前图片的ViewModel
    /// </summary>
    [ObservableProperty] private ImageViewerPageViewModel _currentImage = null!;

    #endregion

    #region Helper Functions

    public string? NextButtonText => NextButtonAction switch
    {
        true => EntryViewerPageResources.NextPageOrIllustration,
        false => EntryViewerPageResources.NextIllustration,
        _ => null
    };

    /// <summary>
    /// <see langword="true"/>: next page<br/>
    /// <see langword="false"/>: next illustration<br/>
    /// <see langword="null"/>: none
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0046:转换为条件表达式")]
    public bool? NextButtonAction
    {
        get
        {
            if (CurrentPageIndex < PageCount - 1)
            {
                return true;
            }

            if (CurrentIllustrationIndex < Illustrations.Count - 1)
            {
                return false;
            }

            return null;
        }
    }

    public string? PrevButtonText => PrevButtonAction switch
    {
        true => EntryViewerPageResources.PrevPageOrIllustration,
        false => EntryViewerPageResources.PrevIllustration,
        _ => null
    };

    /// <summary>
    /// <see langword="true"/>: prev page<br/>
    /// <see langword="false"/>: prev illustration<br/>
    /// <see langword="null"/>: none
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Style", "IDE0046:转换为条件表达式")]
    public bool? PrevButtonAction
    {
        get
        {
            if (CurrentPageIndex > 0)
            {
                return true;
            }

            if (CurrentIllustrationIndex > 0)
            {
                return false;
            }

            return null;
        }
    }

    #endregion

    #region Commands

    private void InitializeCommands()
    {
        FullScreenCommand.ExecuteRequested += FullScreenCommandOnExecuteRequested;
    }

    private void FullScreenCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        IsFullScreen = !IsFullScreen;
        FullScreenCommand.GetFullScreenCommand(IsFullScreen);
    }

    public XamlUICommand IllustrationInfoAndCommentsCommand { get; } =
        EntryViewerPageResources.IllustrationInfoAndComments.GetCommand(FontIconSymbol.InfoE946, VirtualKey.F12);

    public XamlUICommand FullScreenCommand { get; } = "".GetCommand(FontIconSymbol.FullScreenE740);

    #endregion
}
