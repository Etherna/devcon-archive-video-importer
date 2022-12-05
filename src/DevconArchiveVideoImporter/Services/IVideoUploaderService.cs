using Etherna.DevconArchiveVideoImporter.Dtos;
using Etherna.DevconArchiveVideoImporter.Models;
using Etherna.ServicesClient.Clients.Index;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    internal interface IVideoUploaderService
    {
        public Task UploadVideoAsync(
            VideoData videoData,
            bool pinVideo,
            bool offerVideo);

        Task<string> UploadMetadataAsync(
            VideoManifestDto videoManifestDto,
            VideoData videoData,
            bool swarmPin);

        Task<string> UploadMetadataAsync(
            MetadataManifestInsertInput videoManifestDto,
            VideoData videoData,
            bool swarmPin);
    }
}
