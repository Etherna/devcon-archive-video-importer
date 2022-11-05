using Etherna.DevconArchiveVideoParser.Models;
using Etherna.ServicesClient.Clients.Index;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    internal interface IVideoUploaderService
    {
        public Task StartUploadAsync(
            VideoData videoData,
            bool pinVideo,
            bool offerVideo);

        public Task<string> UploadMetadataAsync(
            VideoManifestDto videoManifestDto,
            VideoData videoData,
            bool swarmPin);
    }
}
