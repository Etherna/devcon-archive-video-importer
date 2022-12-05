using Etherna.DevconArchiveVideoImporter.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    public interface IVideoDownloadService
    {
        Task DownloadVideoAsync(Uri uri, string filePath, IProgress<(long totalBytesCopied, long fileSize)> progress);
        Task<List<VideoDataResolution>> GetAllResolutionInfoAsync(VideoData videoData);
        Task<string> DownloadThumbnailAsync(string videoId, string tmpFolder);
    }
}
