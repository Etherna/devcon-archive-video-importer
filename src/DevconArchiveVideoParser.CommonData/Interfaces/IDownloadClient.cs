using Etherna.DevconArchiveVideoParser.CommonData.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoParser.CommonData.Interfaces
{
    public interface IDownloadClient
    {
        Task DownloadAsync(Uri uri, string filePath, IProgress<Tuple<long, long>> progress);
        Task<List<VideoUploadData>> DownloadAllResolutionVideoAsync(string url, int? maxFilesize);
        Task<string?> DownloadThumbnailAsync(string? videoId, string tmpFolder);
    }
}
