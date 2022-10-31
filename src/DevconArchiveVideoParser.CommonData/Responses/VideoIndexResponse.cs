using Etherna.DevconArchiveVideoParser.CommonData.Models;

namespace Etherna.DevconArchiveVideoParser.CommonData.Responses
{
    public class VideoIndexResponse
    {
        // Properties.
        public string Id { get; set; } = default!;
        public MetadataVideo? LastValidManifest { get; set; }
    }
}
