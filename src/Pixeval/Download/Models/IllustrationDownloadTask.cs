#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/IllustrationDownloadTask.cs
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

using System.IO;
using System.Threading.Tasks;
using Pixeval.Controls;
using Pixeval.Database;
using Pixeval.Options;
using Pixeval.Util;
using Pixeval.Util.IO;
using Pixeval.Utilities;
using SixLabors.ImageSharp;

namespace Pixeval.Download.Models;

public class IllustrationDownloadTask(DownloadHistoryEntry entry, IllustrationItemViewModel illustration)
    : DownloadTaskBase(entry)
{
    public override IWorkViewModel ViewModel => IllustrationViewModel;

    public IllustrationItemViewModel IllustrationViewModel { get; protected set; } = illustration;

    public override async Task DownloadAsync(Downloader downloadStreamAsync)
    {
        var url = IllustrationViewModel.OriginalStaticUrl!;

        Destination = IoHelper.ReplaceTokenExtensionFromUrl(Destination, url).RemoveTokens();

        await DownloadAsyncCore(downloadStreamAsync, url, Destination);
    }

    protected virtual async Task DownloadAsyncCore(Downloader downloadStreamAsync, string url, string destination)
    {
        if (!App.AppViewModel.AppSettings.OverwriteDownloadedFile && File.Exists(destination))
            return;

        if (App.AppViewModel.AppSettings.UseFileCache && await App.AppViewModel.Cache.TryGetAsync<Stream>(MakoHelper.GetOriginalCacheKey(url)) is { } stream)
        {
            await using (stream)
                await ManageStream(stream, url, destination);
            return;
        }

        if (await downloadStreamAsync(url, this, CancellationHandle) is Result<Stream>.Success result)
        {
            await using var stream2 = result.Value;
            await ManageStream(stream2, url, destination);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="stream">会自动Dispose</param>
    /// <param name="url"></param>
    /// <param name="destination"></param>
    /// <returns></returns>
    protected virtual async Task ManageStream(Stream stream, string url, string destination)
    {
        if (App.AppViewModel.AppSettings.IllustrationDownloadFormat is IllustrationDownloadFormat.Original)
        {
            await stream.StreamSaveToFileAsync(destination);
        }
        else
        {
            using var image = await Image.LoadAsync(stream);
            image.SetTags(IllustrationViewModel.Entry);
            await image.IllustrationSaveToFileAsync(destination);
        }
    }
}
