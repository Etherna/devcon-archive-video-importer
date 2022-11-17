using Etherna.ServicesClient.Clients.Index;

namespace Etherna.DevconArchiveVideoImporter.Dtos
{
    internal class ImageInsertRequest : ImageDto
    {
        public string V { get; set; } = default!;
    }
}
