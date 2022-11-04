using Etherna.DevconArchiveVideoImporter.Index.Models;

namespace Etherna.DevconArchiveVideoImporter.Responses
{
    public class VideoIndexResponse
    {
        // Properties.
        public string Id { get; set; } = default!;
        public MetadataVideo? LastValidManifest { get; set; }
    }
}
