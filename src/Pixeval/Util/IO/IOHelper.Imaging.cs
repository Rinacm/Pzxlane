#region Copyright (c) Pixeval/Pixeval
// GPL v3 License
// 
// Pixeval/Pixeval
// Copyright (c) 2023 Pixeval/IOHelper.Imaging.cs
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
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.FileProperties;
using Microsoft.UI.Xaml.Media.Imaging;
using Pixeval.AppManagement;
using Pixeval.CoreApi.Net.Response;
using Pixeval.Download.Macros;
using Pixeval.Options;
using Pixeval.Utilities;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using WinUI3Utilities;
using QRCoder;

namespace Pixeval.Util.IO;

public static partial class IoHelper
{
    public static async Task<SoftwareBitmapSource> GetSoftwareBitmapSourceAsync(this Stream stream, bool disposeOfImageStream)
    {
        try
        {
            // 此处Position可能为负数
            stream.Position = 0;
            using var image = await Image.LoadAsync<Bgra32>(stream);
            var softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, image.Width, image.Height, BitmapAlphaMode.Premultiplied);
            var buffer = new byte[4 * image.Width * image.Height];
            image.CopyPixelDataTo(buffer);
            softwareBitmap.CopyFromBuffer(buffer.AsBuffer());
            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap);
            return source;
            // BitmapDecoder Bug多
            // var decoder = await BitmapDecoder.CreateAsync(imageStream);
            // return await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
        catch
        {
            return await AppInfo.GetNotAvailableImageAsync();
        }
        finally
        {
            if (disposeOfImageStream)
                await Functions.IgnoreExceptionAsync(stream.DisposeAsync);
        }
    }

    public static async Task<BitmapImage> GetBitmapImageAsync(this Stream imageStream, bool disposeOfImageStream, int? desiredWidth = null)
    {
        var bitmapImage = new BitmapImage
        {
            DecodePixelType = DecodePixelType.Logical
        };
        if (desiredWidth is { } width)
            bitmapImage.DecodePixelWidth = width;
        await bitmapImage.SetSourceAsync(imageStream.AsRandomAccessStream());
        if (disposeOfImageStream)
            await imageStream.DisposeAsync();

        return bitmapImage;
    }

    public static async Task<Stream> GetFileThumbnailAsync(string path, uint size = 64)
    {
        var file = await StorageFile.GetFileFromPathAsync(path);
        var thumbnail = await file.GetThumbnailAsync(ThumbnailMode.SingleItem, size);
        return thumbnail.AsStreamForRead();
    }

    public static async Task UgoiraSaveToFileAsync(this Image image, string path, UgoiraDownloadFormat? ugoiraDownloadFormat = null)
    {
        CreateParentDirectories(path);
        await using var fileStream = File.OpenWrite(path);
        _ = await UgoiraSaveToStreamAsync(image, fileStream, ugoiraDownloadFormat);
    }

    /// <summary>
    /// Writes the frames that are contained in <paramref name="streams" /> into <see cref="Stream"/>
    /// and encodes to <paramref name="ugoiraDownloadFormat"/> format
    /// </summary>
    public static async Task<Stream> UgoiraSaveToStreamAsync(this IEnumerable<Stream> streams, IEnumerable<int> delays, Stream? target = null, IProgress<int>? progress = null, UgoiraDownloadFormat? ugoiraDownloadFormat = null)
    {
        using var image = await streams.UgoiraSaveToImageAsync(delays, progress);
        var s = await image.UgoiraSaveToStreamAsync(target ?? _recyclableMemoryStreamManager.GetStream());
        progress?.Report(100);
        return s;
    }

    public static async Task<T> UgoiraSaveToStreamAsync<T>(this Image image, T destination, UgoiraDownloadFormat? ugoiraDownloadFormat = null) where T : Stream
    {
        return await Task.Run(async () =>
        {
            ugoiraDownloadFormat ??= App.AppViewModel.AppSettings.UgoiraDownloadFormat;
            await image.SaveAsync(destination,
                ugoiraDownloadFormat switch
                {
                    UgoiraDownloadFormat.Tiff => new TiffEncoder(),
                    UgoiraDownloadFormat.APng => new PngEncoder(),
                    UgoiraDownloadFormat.Gif => new GifEncoder(),
                    UgoiraDownloadFormat.WebPLossless => new WebpEncoder { FileFormat = WebpFileFormatType.Lossless },
                    UgoiraDownloadFormat.WebPLossy => new WebpEncoder { FileFormat = WebpFileFormatType.Lossy },
                    _ => ThrowHelper.ArgumentOutOfRange<UgoiraDownloadFormat?, IImageEncoder>(ugoiraDownloadFormat)
                });
            image.Dispose();
            destination.Position = 0;
            return destination;
        });
    }

    public static async Task<Image> UgoiraSaveToImageAsync(this IEnumerable<Stream> streams, IEnumerable<int> delays, IProgress<int>? progress = null)
    {
        return await Task.Run(async () =>
        {
            var s = streams as IList<Stream> ?? streams.ToArray();
            var average = 50d / s.Count;
            var d = delays as IList<int> ?? delays.ToArray();
            var progressValue = 0d;

            var images = new Image[s.Count];
            await Parallel.ForAsync(0, s.Count, async (i, token) =>
            {
                var delay = d.Count > i ? (uint)d[i] : 10u;
                s[i].Position = 0;
                images[i] = await Image.LoadAsync(s[i], token);
                images[i].Frames[0].Metadata.GetFormatMetadata(WebpFormat.Instance).FrameDelay = delay;
                progressValue += average;
                progress?.Report((int)progressValue);
            });

            var image = images[0];
            foreach (var img in images.Skip(1))
            {
                using (img)
                    _ = image.Frames.AddFrame(img.Frames[0]);
                progressValue += average / 2;
                progress?.Report((int)progressValue);
            }

            return image;
        });
    }

    public static async Task IllustrationSaveToFileAsync(this Image image, string path, IllustrationDownloadFormat? illustrationDownloadFormat = null)
    {
        CreateParentDirectories(path);
        await using var fileStream = File.OpenWrite(path);
        _ = await IllustrationSaveToStreamAsync(image, fileStream, illustrationDownloadFormat);
    }

    public static async Task StreamSaveToFileAsync(this Stream stream, string path)
    {
        CreateParentDirectories(path);
        await using var fileStream = File.OpenWrite(path);
        await stream.CopyToAsync(fileStream);
    }

    public static async Task<T> IllustrationSaveToStreamAsync<T>(this Image image, T destination, IllustrationDownloadFormat? illustrationDownloadFormat = null) where T : Stream
    {
        return await Task.Run(async () =>
        {
            illustrationDownloadFormat ??= App.AppViewModel.AppSettings.IllustrationDownloadFormat;
            await image.SaveAsync(destination,
                illustrationDownloadFormat switch
                {
                    IllustrationDownloadFormat.Jpg => new JpegEncoder(),
                    IllustrationDownloadFormat.Png => new PngEncoder(),
                    IllustrationDownloadFormat.Bmp => new BmpEncoder(),
                    IllustrationDownloadFormat.WebPLossless => new WebpEncoder { FileFormat = WebpFileFormatType.Lossless },
                    IllustrationDownloadFormat.WebPLossy => new WebpEncoder { FileFormat = WebpFileFormatType.Lossy },
                    _ => ThrowHelper.ArgumentOutOfRange<IllustrationDownloadFormat?, IImageEncoder>(illustrationDownloadFormat)
                });
            image.Dispose();
            destination.Position = 0;
            return destination;
        });
    }

    public static string GetUgoiraExtension(UgoiraDownloadFormat? ugoiraDownloadFormat = null)
    {
        ugoiraDownloadFormat ??= App.AppViewModel.AppSettings.UgoiraDownloadFormat;
        return ugoiraDownloadFormat switch
        {
            UgoiraDownloadFormat.OriginalZip => ".zip",
            UgoiraDownloadFormat.Tiff or UgoiraDownloadFormat.APng or UgoiraDownloadFormat.Gif => "." + ugoiraDownloadFormat.ToString()!.ToLower(),
            UgoiraDownloadFormat.WebPLossless or UgoiraDownloadFormat.WebPLossy => ".webp",
            _ => ThrowHelper.ArgumentOutOfRange<UgoiraDownloadFormat?, string>(ugoiraDownloadFormat)
        };
    }

    public static string GetIllustrationExtension(IllustrationDownloadFormat? illustrationDownloadFormat = null)
    {
        illustrationDownloadFormat ??= App.AppViewModel.AppSettings.IllustrationDownloadFormat;
        return illustrationDownloadFormat switch
        {
            IllustrationDownloadFormat.Original => FileExtensionMacro.NameConstToken,
            IllustrationDownloadFormat.Jpg or IllustrationDownloadFormat.Png or IllustrationDownloadFormat.Bmp => "." + illustrationDownloadFormat.ToString()!.ToLower(),
            IllustrationDownloadFormat.WebPLossless or IllustrationDownloadFormat.WebPLossy => ".webp",
            _ => ThrowHelper.ArgumentOutOfRange<IllustrationDownloadFormat?, string>(illustrationDownloadFormat)
        };
    }

    public static string GetNovelExtension(NovelDownloadFormat? novelDownloadFormat = null)
    {
        novelDownloadFormat ??= App.AppViewModel.AppSettings.NovelDownloadFormat;
        return novelDownloadFormat switch
        {
            NovelDownloadFormat.OriginalTxt => ".txt",
            NovelDownloadFormat.Pdf or NovelDownloadFormat.Html or NovelDownloadFormat.Md => "." + novelDownloadFormat.ToString()!.ToLower(),
            _ => ThrowHelper.ArgumentOutOfRange<NovelDownloadFormat?, string>(novelDownloadFormat)
        };
    }

    public static async Task<Image> GetImageFromZipStreamAsync(Stream zipStream, UgoiraMetadataResponse ugoiraMetadataResponse)
    {
        var entryStreams = await ReadZipArchiveEntriesAsync(zipStream, true);
        return await entryStreams.UgoiraSaveToImageAsync(ugoiraMetadataResponse.UgoiraMetadataInfo.Frames.Select(t => (int)t.Delay));
    }

    public static async Task<SoftwareBitmapSource> GenerateQrCodeForUrlAsync(string url)
    {
        var qrCodeGen = new QRCodeGenerator();
        var urlPayload = new PayloadGenerator.Url(url);
        var qrCodeData = qrCodeGen.CreateQrCode(urlPayload, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new BitmapByteQRCode(qrCodeData);
        var bytes = qrCode.GetGraphic(20);
        return await _recyclableMemoryStreamManager.GetStream(bytes).GetSoftwareBitmapSourceAsync(true);
    }

    public static async Task<SoftwareBitmapSource> GenerateQrCodeAsync(string content)
    {
        var qrCodeGen = new QRCodeGenerator();
        var qrCodeData = qrCodeGen.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        var qrCode = new BitmapByteQRCode(qrCodeData);
        var bytes = qrCode.GetGraphic(20);
        return await _recyclableMemoryStreamManager.GetStream(bytes).GetSoftwareBitmapSourceAsync(true);
    }

    public static string ChangeExtensionFromUrl(string path, string url)
    {
        var index = url.LastIndexOf('.');
        return Path.ChangeExtension(path, url[index..]);
    }

    public static string ReplaceTokenExtensionFromUrl(string path, string url)
    {
        var index = url.LastIndexOf('.');
        return path.Replace(FileExtensionMacro.NameConstToken, url[index..]);
    }

    public static string RemoveTokens(this string path)
    {
        // .Replace(FileExtensionMacro.NameConstToken, "")
        return path.Replace(MangaIndexMacro.NameConstToken, "");
    }
}
