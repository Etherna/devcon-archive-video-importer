using Etherna.ServicesClient.Clients.Index;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    public interface IEthernaClientService
    {
        Task<string> CreateBatchAsync();
        Task<VideoManifestDto?> GetLastValidManifestAsync(string? videoId);
        Task<SystemParametersDto> GetParamsInfoAsync();
        Task<string> GetBatchIdFromReferenceAsync(string referenceId);
        Task<bool> IsUsableBatchAsync(string batchId);
        Task OfferResourceAsync(string hash);
    }
}
