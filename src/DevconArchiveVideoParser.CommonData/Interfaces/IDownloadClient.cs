using Etherna.DevconArchiveVideoParser.CommonData.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoParser.CommonData.Interfaces
{
    public interface IDownloadClient
    {
        Task DownloadAsync(Uri uri, string filePath, IProgress<(long totalBytesCopied, long fileSize)> progress);
        Task<List<VideoDataItem>> DownloadAllResolutionVideoAsync(MDFileData mdFileData, int? maxFilesize);
        Task<string> DownloadThumbnailAsync(string videoId, string tmpFolder);
    }
}
