using Etherna.DevconArchiveVideoParser.CommonData.Models;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoParser.Services
{
    internal interface IVideoUploaderService
    {
        public Task StartUploadAsync(
            VideoData videoUploadData,
            bool pinVideo,
            bool offerVideo);

        public Task<string> UploadMetadataAsync(
            MetadataVideo metadataVideo,
            MDFileData mdFileData,
            bool swarmPin);
    }
}
