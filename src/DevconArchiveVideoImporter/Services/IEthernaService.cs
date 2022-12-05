using Etherna.ServicesClient.Clients.Index;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    public interface IEthernaService
    {
        Task<string> CreateBatchAsync();
        Task<VideoManifestDto?> GetLastValidManifestAsync(string? videoId);
        Task<SystemParametersDto> GetInfoAsync();
        Task<string> GetBatchIdFromBatchReferenceAsync(string referenceId);
        Task<bool> IsBatchUsableAsync(string batchId);
        Task OfferResourceAsync(string hash);
    }
}
