#region Copyright

// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2024 Pixeval/PdfParserViewModel.cs
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
using System.IO;
using System.Threading.Tasks;
using Pixeval.AppManagement;
using Pixeval.CoreApi.Model;
using Pixeval.Util.IO;
using Pixeval.Util;
using Pixeval.Utilities.Threading;
using Pixeval.Utilities;
using System.Text;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace Pixeval.Controls;

public class FileParserViewModel(NovelContent novelContent) : INovelParserViewModel<Stream>
{
    public NovelContent NovelContent { get; } = novelContent;

    public Dictionary<long, NovelIllustInfo> IllustrationLookup { get; } = [];

    public Dictionary<(long, int), Stream> IllustrationImages { get; } = [];

    public Dictionary<long, Stream> UploadedImages { get; } = [];

    public async Task<StringBuilder> LoadMdContentAsync()
    {
        await LoadImagesAsync();

        var index = 0;
        var length = NovelContent.Text.Length;

        var a = new StringBuilder();
        for (var i = 0; index < length; ++i)
        {
            var parser = new PixivNovelMdParser(a, i);
            _ = parser.Parse(NovelContent.Text, ref index, this);
            if (LoadingCancellationHandle.IsCancelled)
                break;
        }

        return a;
    }

    public async Task<StringBuilder> LoadHtmlContentAsync()
    {
        await LoadImagesAsync();

        var index = 0;
        var length = NovelContent.Text.Length;

        var a = new StringBuilder();
        for (var i = 0; index < length; ++i)
        {
            var parser = new PixivNovelHtmlParser(a, i);
            _ = parser.Parse(NovelContent.Text, ref index, this);
            if (LoadingCancellationHandle.IsCancelled)
                break;
        }

        return a;
    }

    public async Task<Document> LoadPdfContentAsync()
    {
        await LoadImagesAsync();

        var index = 0;
        var length = NovelContent.Text.Length;

        PixivNovelPdfParser.Init();

        return
            Document.Create(t =>
                t.Page(p =>
                {
                    p.MarginHorizontal(90);
                    p.MarginVertical(72);
                    p.DefaultTextStyle(new TextStyle().LineHeight(2));
                    p.Content().Column(c =>
                    {
                        for (var i = 0; index < length; ++i)
                        {
                            var parser = new PixivNovelPdfParser(c, i);
                            _ = parser.Parse(NovelContent.Text, ref index, this);
                            if (LoadingCancellationHandle.IsCancelled)
                                break;
                        }
                    });
                }));
    }

    public async Task LoadImagesAsync()
    {
        foreach (var illust in NovelContent.Illusts)
        {
            IllustrationLookup[illust.Id] = illust;
            IllustrationImages[(illust.Id, illust.Page)] = null!;
        }

        foreach (var image in NovelContent.Images)
            UploadedImages[image.NovelImageId] = null!;

        foreach (var illust in NovelContent.Illusts)
        {
            if (LoadingCancellationHandle.IsCancelled)
                break;
            var key = (illust.Id, illust.Page);
            IllustrationImages[key] = await LoadThumbnailAsync(illust.Illust.Images.Medium);
        }

        foreach (var image in NovelContent.Images)
        {
            if (LoadingCancellationHandle.IsCancelled)
                break;
            UploadedImages[image.NovelImageId] = await LoadThumbnailAsync(image.Urls.X1200);
        }
    }

    private async Task<Stream> LoadThumbnailAsync(string url)
    {
        var cacheKey = MakoHelper.GetCacheKeyForThumbnailAsync(url);

        if (App.AppViewModel.AppSettings.UseFileCache && await App.AppViewModel.Cache.TryGetAsync<Stream>(cacheKey) is { } stream)
        {
            return stream;
        }

        var s = await GetThumbnailAsync(url);
        if (App.AppViewModel.AppSettings.UseFileCache)
            await App.AppViewModel.Cache.AddAsync(cacheKey, s, TimeSpan.FromDays(1));
        return s;
    }

    private CancellationHandle LoadingCancellationHandle { get; } = new();

    /// <summary>
    /// 直接获取对应缩略图
    /// </summary>
    public async Task<Stream> GetThumbnailAsync(string url)
    {
        return await App.AppViewModel.MakoClient.DownloadStreamAsync(url, cancellationHandle: LoadingCancellationHandle) is
            Result<Stream>.Success(var stream)
            ? stream
            : AppInfo.GetNotAvailableImageStream();
    }

    public void Dispose()
    {
        LoadingCancellationHandle.Cancel();
        foreach (var (_, value) in IllustrationImages)
            value?.Dispose();
        foreach (var (_, value) in UploadedImages)
            value?.Dispose();
    }
}
