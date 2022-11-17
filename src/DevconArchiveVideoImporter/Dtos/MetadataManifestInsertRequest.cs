using Etherna.ServicesClient.Clients.Index;

namespace Etherna.DevconArchiveVideoImporter.Dtos
{
    internal class MetadataManifestInsertRequest : VideoManifestDto
    {
        public long CreatedAt { get; set; }
        public string OwnerAddress { get; set; } = default!;
        public long? UpdatedAt { get; set; }
        public string V { get; set; } = default!;
    }
}
