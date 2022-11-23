using Etherna.DevconArchiveVideoImporter.Models;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    internal interface IVideoDownloaderService
    {
        Task<VideoData> StartDownloadAsync(VideoData videoData);
    }
}
