using Etherna.DevconArchiveVideoParser.CommonData.Models;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoParser.Services
{
    internal interface IVideoDownloaderService
    {
        Task<VideoData> StartDownloadAsync(MDFileData videoMd);
    }
}
