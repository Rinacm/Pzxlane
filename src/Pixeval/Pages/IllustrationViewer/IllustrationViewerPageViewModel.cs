#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2022 Pixeval/IllustrationViewerPageViewModel.cs
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
using System.Collections.Immutable;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Pixeval.AppManagement;
using Pixeval.CoreApi.Model;
using Pixeval.Download;
using Pixeval.UserControls;
using Pixeval.UserControls.IllustrationView;
using Pixeval.Util;
using Pixeval.Util.IO;
using Pixeval.Util.UI;
using Pixeval.Utilities;
using Windows.System;
using Windows.System.UserProfile;
using WinUI3Utilities;
using AppContext = Pixeval.AppManagement.AppContext;

namespace Pixeval.Pages.IllustrationViewer;

public partial class IllustrationViewerPageViewModel : DetailedObservableObject, IDisposable
{
    public const string GenerateLink = nameof(GenerateLink);
    public const string ShowShare = nameof(ShowShare);
    public const string ShowQrCode = nameof(ShowQrCode);

    [ObservableProperty]
    private bool _isInfoPaneOpen;

    // The reason why we don't put UserProfileImageSource into IllustrationViewModel
    // is because the whole array of Illustrations is just representing the same 
    // illustration's different manga pages, so all of them have the same illustrator
    // If the UserProfileImageSource is in IllustrationViewModel and the illustration
    // itself is a manga then all of the IllustrationViewModel in Illustrations will
    // request the same user profile image which is pointless and will (inevitably) causing
    // the waste of system resource
    [ObservableProperty]
    private ImageSource? _userProfileImageSource;

    // Preserved for illustrator view use
    [ObservableProperty]
    private UserInfo? _userInfo;

    public TeachingTip SnackBarTeachingTip { get; set; } = null!;

    public TeachingTip GenerateLinkTeachingTip { get; set; } = null!;

    public TeachingTip ShowQrCodeTeachingTip { get; set; } = null!;

    private bool _isFullScreen;

    public bool IsFullScreen
    {
        get => _isFullScreen;
        set
        {
            if (value == _isFullScreen)
                return;
            _isFullScreen = value;
            if (value)
            {
                FullScreenCommand.Description = IllustrationViewerPageResources.BackToWindow;
                FullScreenCommand.IconSource = new SymbolIconSource { Symbol = Symbol.BackToWindow };
            }
            else
            {
                FullScreenCommand.Description = IllustrationViewerPageResources.FullScreen;
                FullScreenCommand.IconSource = new SymbolIconSource { Symbol = Symbol.FullScreen };
            }
            OnPropertyChanged();
        }
    }

    #region Current

    public ImmutableArray<IllustrationViewModel> Illustrations => ViewModel.DataProvider.View.Cast<IllustrationViewModel>().ToImmutableArray();

    public IllustrationViewModel CurrentIllustration => Illustrations[CurrentIllustrationIndex];

    public IllustrationViewModel CurrentPage => _pages[CurrentPageIndex];

    public int CurrentIllustrationIndex
    {
        get => _currentIllustrationIndex;
        set
        {
            var oldValue = _currentIllustrationIndex;
            var oldTag = _pages?[CurrentPageIndex].Id ?? "";

            _currentIllustrationIndex = value;
            _pages = CurrentIllustration.Illustrate.PageCount <= 1
                ? new[] { CurrentIllustration }
                : CurrentIllustration.Illustrate.MetaPages!
                    .Select((m, i) =>
                        new IllustrationViewModel(CurrentIllustration.Illustrate with { ImageUrls = m.ImageUrls })
                        {
                            MangaIndex = i
                        }).ToArray();

            CurrentPageIndex = 0;
            OnDetailedPropertyChanged(oldValue, value, oldTag, CurrentPage.Id);
        }
    }

    public int CurrentPageIndex
    {
        get => _currentPageIndex;
        set
        {
            _currentPageIndex = value;
            OnPropertyChanged(nameof(NextButtonEnable));
            OnPropertyChanged(nameof(PrevButtonEnable));
            CurrentImage = new(this, CurrentPage);
        }
    }

    private int _currentIllustrationIndex;
    private int _currentPageIndex;

    private IllustrationViewModel[] _pages = null!;

    [ObservableProperty]
    private ImageViewerPageViewModel _currentImage = null!;

    #endregion

    private readonly SharedRef<IllustrationViewViewModel> _viewModelRef;

    public SharedRef<IllustrationViewViewModel> ViewModelRef => _viewModelRef.MakeShared();

    public IllustrationViewViewModel ViewModel => _viewModelRef.Value;

    // illustrations should contains only one item if the illustration is a single
    // otherwise it contains the entire manga data
    public IllustrationViewerPageViewModel(SharedRef<IllustrationViewViewModel> viewModelRef, int currentIllustrationIndex)
    {
        _viewModelRef = viewModelRef;
        ViewModel.DataProvider.FilterChanged += (_, _) =>
        {
            CurrentIllustrationIndex = Illustrations.IndexOf(CurrentIllustration);
        };
        CurrentIllustrationIndex = currentIllustrationIndex;

        InitializeCommands();
        _ = LoadUserProfile();
    }

    public ImageViewerPageViewModel NextPage()
    {
        ++CurrentPageIndex;
        return CurrentImage;
    }

    public ImageViewerPageViewModel PrevPage()
    {
        --CurrentPageIndex;
        return CurrentImage;
    }

    #region Helper Functions

    public Visibility NextButtonEnable => NextButtonAction is null ? Visibility.Collapsed : Visibility.Visible;

    public bool NextIllustrationEnable => Illustrations.Length > CurrentIllustrationIndex + 1;

    /// <summary>
    /// <see langword="true"/>: next page<br/>
    /// <see langword="false"/>: next illustration<br/>
    /// <see langword="null"/>: none
    /// </summary>
    public bool? NextButtonAction
    {
        get
        {
            if (CurrentPageIndex < _pages.Length - 1)
            {
                return true;
            }

            if (NextIllustrationEnable)
            {
                return false;
            }

            return null;
        }
    }

    public Visibility PrevButtonEnable => PrevButtonAction is null ? Visibility.Collapsed : Visibility.Visible;

    public bool PrevIllustrationEnable => CurrentIllustrationIndex > 0;

    /// <summary>
    /// <see langword="true"/>: prev page<br/>
    /// <see langword="false"/>: prev illustration<br/>
    /// <see langword="null"/>: none
    /// </summary>
    public bool? PrevButtonAction
    {
        get
        {
            if (CurrentPageIndex > 0)
            {
                return true;
            }

            if (PrevIllustrationEnable)
            {
                return false;
            }

            return null;
        }
    }

    #endregion

    public Task PostPublicBookmarkAsync()
    {
        // changes the IsBookmarked property of the item that of in the thumbnail list
        // so the thumbnail item will also receive state update 
        CurrentIllustration.IsBookmarked = true;

        return CurrentIllustration.PostPublicBookmarkAsync();
    }

    public Task RemoveBookmarkAsync()
    {
        CurrentIllustration.IsBookmarked = false;

        return CurrentIllustration.RemoveBookmarkAsync();
    }

    public async Task LoadUserProfile()
    {
        if (CurrentIllustration.Illustrate.User is { } userInfo && UserProfileImageSource is null)
        {
            UserInfo = userInfo;
            if (userInfo.ProfileImageUrls?.Medium is { } profileImage)
            {
                UserProfileImageSource = await App.AppViewModel.MakoClient.DownloadSoftwareBitmapSourceResultAsync(profileImage)
                    .GetOrElseAsync(await AppContext.GetPixivNoProfileImageAsync());
            }
        }
    }

    public string IllustrationId => CurrentIllustration.Illustrate.Id.ToString();

    public string? IllustratorName => CurrentIllustration.Illustrate.User?.Name;

    public string? IllustratorUid => CurrentIllustration.Illustrate.User?.Id.ToString();

    public bool IsManga => _pages.Length > 1;

    public bool IsUgoira => CurrentIllustration.Illustrate.IsUgoira();

    #region Commands

    public void UpdateCommandCanExecute()
    {
        PlayGifCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        RestoreResolutionCommand.NotifyCanExecuteChanged();
        ZoomInCommand.NotifyCanExecuteChanged();
        ZoomOutCommand.NotifyCanExecuteChanged();
        FullScreenCommand.NotifyCanExecuteChanged();
    }

    private void InitializeCommands()
    {
        BookmarkCommand =
            (CurrentIllustration.IsBookmarked ? MiscResources.RemoveBookmark : MiscResources.AddBookmark)
            .GetCommand(
                MakoHelper.GetBookmarkButtonIconSource(CurrentIllustration.IsBookmarked),
                VirtualKeyModifiers.Control, VirtualKey.D);

        RestoreResolutionCommand.CanExecuteRequested += LoadingCompletedCanExecuteRequested;

        BookmarkCommand.ExecuteRequested += BookmarkCommandOnExecuteRequested;

        CopyCommand.CanExecuteRequested += LoadingCompletedCanExecuteRequested;
        CopyCommand.ExecuteRequested += CopyCommandOnExecuteRequested;

        PlayGifCommand.CanExecuteRequested += PlayGifCommandOnCanExecuteRequested;
        PlayGifCommand.ExecuteRequested += PlayGifCommandOnExecuteRequested;

        ZoomOutCommand.CanExecuteRequested += LoadingCompletedCanExecuteRequested;
        ZoomOutCommand.ExecuteRequested += (_, _) => CurrentImage.Zoom(-120);

        ZoomInCommand.CanExecuteRequested += LoadingCompletedCanExecuteRequested;
        ZoomInCommand.ExecuteRequested += (_, _) => CurrentImage.Zoom(120);

        SaveCommand.ExecuteRequested += SaveCommandOnExecuteRequested;
        SaveAsCommand.ExecuteRequested += SaveAsCommandOnExecuteRequested;

        GenerateLinkCommand.ExecuteRequested += GenerateLinkCommandOnExecuteRequested;
        GenerateWebLinkCommand.ExecuteRequested += GenerateWebLinkCommandOnExecuteRequested;
        OpenInWebBrowserCommand.ExecuteRequested += OpenInWebBrowserCommandOnExecuteRequested;

        ShareCommand.ExecuteRequested += ShareCommandExecuteRequested;
        ShowQrCodeCommand.ExecuteRequested += ShowQrCodeCommandExecuteRequested;

        SetAsLockScreenCommand.CanExecuteRequested += SetAsLockScreenCommandOnCanExecuteRequested;
        SetAsLockScreenCommand.ExecuteRequested += SetAsLockScreenCommandOnExecuteRequested;

        SetAsBackgroundCommand.CanExecuteRequested += SetAsBackgroundCommandOnCanExecuteRequested;
        SetAsBackgroundCommand.ExecuteRequested += SetAsBackgroundCommandOnExecuteRequested;

        FullScreenCommand.ExecuteRequested += (_, _) => IsFullScreen = !IsFullScreen;

        RestoreResolutionCommand.ExecuteRequested += FlipRestoreResolutionCommand;
    }

    private void LoadingCompletedCanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
    {
        args.CanExecute = CurrentImage.LoadingCompletedSuccessfully;
    }

    private async void SetAsBackgroundCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        if (CurrentImage.OriginalImageStream is null)
        {
            SnackBarTeachingTip.ShowAndHide(IllustrationViewerPageResources.OriginalmageStreamIsEmptyContent, TeachingTipSeverity.Error);
            return;
        }

        var guid = await CurrentImage.OriginalImageStream.Sha1Async();
        if (await AppKnownFolders.SavedWallPaper.TryGetFileRelativeToSelfAsync(guid) is null)
        {
            var path = Path.Combine(AppKnownFolders.SavedWallPaper.Self.Path, guid);
            using var scope = App.AppViewModel.AppServicesScope;
            var factory = scope.ServiceProvider.GetRequiredService<IDownloadTaskFactory<IllustrationViewModel, ObservableDownloadTask>>();
            var intrinsicTask = await factory.TryCreateIntrinsicAsync(CurrentIllustration, CurrentImage.OriginalImageStream!, path);
            App.AppViewModel.DownloadManager.QueueTask(intrinsicTask);
            await intrinsicTask.Completion.Task;
        }

        await UserProfilePersonalizationSettings.Current.TrySetWallpaperImageAsync(await AppKnownFolders.SavedWallPaper.GetFileAsync(guid));
        ToastNotificationHelper.ShowTextToastNotification(
            IllustrationViewerPageResources.SetAsSucceededTitle,
            IllustrationViewerPageResources.SetAsBackgroundSucceededTitle,
            AppContext.AppLogoNoCaptionUri);
    }

    private async void SetAsLockScreenCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        if (CurrentImage.OriginalImageStream is null)
        {
            SnackBarTeachingTip.ShowAndHide(IllustrationViewerPageResources.OriginalmageStreamIsEmptyContent, TeachingTipSeverity.Error);
            return;
        }

        var guid = await CurrentImage.OriginalImageStream.Sha1Async();
        if (await AppKnownFolders.SavedWallPaper.TryGetFileRelativeToSelfAsync(guid) is null)
        {
            var path = Path.Combine(AppKnownFolders.SavedWallPaper.Self.Path, guid);
            using var scope = App.AppViewModel.AppServicesScope;
            var factory = scope.ServiceProvider.GetRequiredService<IDownloadTaskFactory<IllustrationViewModel, ObservableDownloadTask>>();
            var intrinsicTask = await factory.TryCreateIntrinsicAsync(CurrentIllustration, CurrentImage.OriginalImageStream!, path);
            App.AppViewModel.DownloadManager.QueueTask(intrinsicTask);
            await intrinsicTask.Completion.Task;
        }

        await UserProfilePersonalizationSettings.Current.TrySetLockScreenImageAsync(await AppKnownFolders.SavedWallPaper.GetFileAsync(guid));
        ToastNotificationHelper.ShowTextToastNotification(
            IllustrationViewerPageResources.SetAsSucceededTitle,
            IllustrationViewerPageResources.SetAsBackgroundSucceededTitle,
            AppContext.AppLogoNoCaptionUri);
    }

    private void SetAsBackgroundCommandOnCanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
    {
        args.CanExecute = !IsUgoira && CurrentImage.LoadingCompletedSuccessfully;
    }

    private void SetAsLockScreenCommandOnCanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
    {
        args.CanExecute = !IsUgoira && CurrentImage.LoadingCompletedSuccessfully;
    }

    private void GenerateLinkCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        if (App.AppViewModel.AppSetting.DisplayTeachingTipWhenGeneratingAppLink)
        {
            OnPropertyChanged(nameof(GenerateLink));
            GenerateLinkTeachingTip.IsOpen = true;
        }

        UIHelper.ClipboardSetText(MakoHelper.GenerateIllustrationAppUri(CurrentIllustration.Id).ToString());
    }

    private void GenerateWebLinkCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        var link = MakoHelper.GenerateIllustrationWebUri(CurrentIllustration.Id).ToString();
        UIHelper.ClipboardSetText(link);
        SnackBarTeachingTip.ShowAndHide(IllustrationViewerPageResources.WebLinkCopiedToClipboardToastTitle);
    }

    private async void OpenInWebBrowserCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        await Launcher.LaunchUriAsync(MakoHelper.GenerateIllustrationWebUri(CurrentIllustration.Id));
    }

    private void ShareCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        if (CurrentImage.LoadingOriginalSourceTask is not { IsCompletedSuccessfully: true })
        {
            SnackBarTeachingTip.ShowAndHide(IllustrationViewerPageResources.CannotShareImageForNowTitle, TeachingTipSeverity.Warning,
                IllustrationViewerPageResources.CannotShareImageForNowContent);
            return;
        }
        OnPropertyChanged(nameof(ShowShare));
    }

    private async void ShowQrCodeCommandExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        var qrCodeSource = await UIHelper.GenerateQrCodeForUrlAsync(MakoHelper.GenerateIllustrationWebUri(CurrentIllustration.Id).ToString());

        ShowQrCodeTeachingTip.HeroContent.To<Image>().Source = qrCodeSource;
        OnPropertyChanged(nameof(ShowQrCode));
        ShowQrCodeTeachingTip.IsOpen = true;

        void Closed(TeachingTip s, TeachingTipClosedEventArgs ea)
        {
            qrCodeSource.Dispose();
            s.Closed -= Closed;
        }

        ShowQrCodeTeachingTip.Closed += Closed;
    }

    private async void SaveAsCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        await CurrentIllustration.SaveAsAsync();
    }

    private async void SaveCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        await CurrentIllustration.SaveAsync();
    }

    private void BookmarkCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        if (CurrentIllustration.IsBookmarked)
            RemoveBookmarkAsync();
        else
            PostPublicBookmarkAsync();
        // update manually
        BookmarkCommand.Label = CurrentIllustration.IsBookmarked ? MiscResources.RemoveBookmark : MiscResources.AddBookmark;
        BookmarkCommand.IconSource = MakoHelper.GetBookmarkButtonIconSource(CurrentIllustration.IsBookmarked);
    }

    private void PlayGifCommandOnCanExecuteRequested(XamlUICommand sender, CanExecuteRequestedEventArgs args)
    {
        args.CanExecute = IsUgoira && (CurrentImage.LoadingOriginalSourceTask?.IsCompletedSuccessfully ?? false);
    }

    private void PlayGifCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        var bitmap = CurrentImage.OriginalImageSource.To<BitmapImage>();
        if (bitmap.IsPlaying)
        {
            bitmap.Stop();
            PlayGifCommand.Description = PlayGifCommand.Label = IllustrationViewerPageResources.PlayGif;
            PlayGifCommand.IconSource = new SymbolIconSource { Symbol = Symbol.Play };
        }
        else
        {
            bitmap.Play();
            PlayGifCommand.Description = PlayGifCommand.Label = IllustrationViewerPageResources.PauseGif;
            PlayGifCommand.IconSource = new SymbolIconSource { Symbol = Symbol.Stop };
        }
    }

    private async void CopyCommandOnExecuteRequested(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        var encoded = await CurrentImage.OriginalImageStream!.EncodeBitmapStreamAsync(false);
        UIHelper.ClipboardSetBitmap(encoded);
    }

    public void FlipRestoreResolutionCommand(XamlUICommand sender, ExecuteRequestedEventArgs args)
    {
        RestoreResolutionCommand.Label = CurrentImage.IsFit ? IllustrationViewerPageResources.UniformToFillResolution : IllustrationViewerPageResources.RestoreOriginalResolution;
        RestoreResolutionCommand.IconSource = CurrentImage.IsFit ? FontIconSymbols.FitPageE9A6.GetFontIconSource() : FontIconSymbols.WebcamE8B8.GetFontIconSource();
        CurrentImage.ShowMode = CurrentImage.ShowMode is ZoomableImageMode.Fit ? ZoomableImageMode.Original : ZoomableImageMode.Fit;
    }

    public XamlUICommand IllustrationInfoAndCommentsCommand { get; } =
        IllustrationViewerPageResources.IllustrationInfoAndComments.GetCommand(FontIconSymbols.InfoE946, VirtualKey.F12);

    public XamlUICommand CopyCommand { get; } = IllustrationViewerPageResources.Copy.GetCommand(
            FontIconSymbols.CopyE8C8, VirtualKeyModifiers.Control, VirtualKey.C);

    // The gif will be played as soon as its loaded, so the default state is playing and thus we need the button to be pause
    public XamlUICommand PlayGifCommand { get; } = IllustrationViewerPageResources.PauseGif.GetCommand(FontIconSymbols.StopE71A);

    public XamlUICommand ZoomOutCommand { get; } = IllustrationViewerPageResources.ZoomOut.GetCommand(
        FontIconSymbols.ZoomOutE71F, VirtualKey.Subtract);

    public XamlUICommand ZoomInCommand { get; } = IllustrationViewerPageResources.ZoomIn.GetCommand(
        FontIconSymbols.ZoomInE8A3, VirtualKey.Add);

    public XamlUICommand BookmarkCommand { get; private set; } = null!; // the null-state is transient

    public XamlUICommand SaveCommand { get; } = IllustrationViewerPageResources.Save.GetCommand(
        FontIconSymbols.SaveE74E, VirtualKeyModifiers.Control, VirtualKey.S);

    public XamlUICommand SaveAsCommand { get; } = IllustrationViewerPageResources.SaveAs.GetCommand(
        FontIconSymbols.SaveAsE792, VirtualKeyModifiers.Control | VirtualKeyModifiers.Shift, VirtualKey.S);

    public XamlUICommand SetAsCommand { get; } = IllustrationViewerPageResources.SetAs.GetCommand(FontIconSymbols.PersonalizeE771);

    public XamlUICommand AddToBookmarkCommand { get; } = IllustrationViewerPageResources.AddToBookmark.GetCommand(FontIconSymbols.BookmarksE8A4);

    public XamlUICommand GenerateLinkCommand { get; } = IllustrationViewerPageResources.GenerateLink.GetCommand(FontIconSymbols.LinkE71B);

    public XamlUICommand GenerateWebLinkCommand { get; } = IllustrationViewerPageResources.GenerateWebLink.GetCommand(FontIconSymbols.PreviewLinkE8A1);

    public XamlUICommand OpenInWebBrowserCommand { get; } = IllustrationViewerPageResources.OpenInWebBrowser.GetCommand(FontIconSymbols.WebSearchF6FA);

    public StandardUICommand ShareCommand { get; } = new(StandardUICommandKind.Share);

    public XamlUICommand ShowQrCodeCommand { get; } = IllustrationViewerPageResources.ShowQRCode.GetCommand(FontIconSymbols.QRCodeED14);

    public XamlUICommand SetAsLockScreenCommand { get; } = new() { Label = IllustrationViewerPageResources.LockScreen };

    public XamlUICommand SetAsBackgroundCommand { get; } = new() { Label = IllustrationViewerPageResources.Background };

    public XamlUICommand FullScreenCommand { get; } = IllustrationViewerPageResources.FullScreen.GetCommand(FontIconSymbols.FullScreenE740);

    public XamlUICommand RestoreResolutionCommand { get; } = IllustrationViewerPageResources.RestoreOriginalResolution.GetCommand(FontIconSymbols.WebcamE8B8);

    #endregion

    public void Dispose()
    {
        _pages?.ForEach(i => i.Dispose());
        (UserProfileImageSource as SoftwareBitmapSource)?.Dispose();
        _viewModelRef.Dispose();
    }
}
