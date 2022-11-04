using Etherna.DevconArchiveVideoImporter.Index.Models;
using Etherna.DevconArchiveVideoParser.Models;
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
            MetadataVideo metadataVideo,
            VideoData videoData,
            bool swarmPin);
    }
}
