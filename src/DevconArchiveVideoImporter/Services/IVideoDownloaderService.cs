using Etherna.DevconArchiveVideoImporter.Models;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    /// <summary>
    /// Downloader services
    /// </summary>
    internal interface IVideoDownloaderService
    {
        /// <summary>
        /// Start download from youtube url.
        /// </summary>
        /// <param name="videoData">video data</param>
        Task<VideoData> StartDownloadAsync(VideoData videoData);
    }
}
