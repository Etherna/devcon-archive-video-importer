using Etherna.DevconArchiveVideoImporter.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    public interface IDownloadClient
    {
        Task DownloadAsync(Uri uri, string filePath, IProgress<(long totalBytesCopied, long fileSize)> progress);
        Task<List<VideoDataResolution>> DownloadAllResolutionVideoAsync(VideoData videoData, int? maxFilesize);
        Task<string> DownloadThumbnailAsync(string videoId, string tmpFolder);
    }
}
