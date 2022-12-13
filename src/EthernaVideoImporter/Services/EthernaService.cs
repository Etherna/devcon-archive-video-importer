using Etherna.EthernaVideoImporter.Models;
using Etherna.ServicesClient;
using Etherna.ServicesClient.Clients.Index;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.EthernaVideoImporter.Services
{
    public class EthernaService : IEthernaService
    {
        // Const.
        private const int BATCH_DEEP = 20;
        private readonly TimeSpan BATCH_DURANTION_TIME = new(365, 0, 0, 0);
        private const int BLOCK_TIME = 5;
        private const int MAX_RETRY = 3;

        // Fields.
        private readonly IEthernaUserClients ethernaUserClients;

        // Constructors.
        public EthernaService(IEthernaUserClients ethernaUserClients)
        {
            this.ethernaUserClients = ethernaUserClients;
        }

        // Methods.
        public async Task<string> AddManifestToIndex(
            string hashReferenceMetadata,
            VideoData videoData)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));

            VideoDto? videoIndexDto = null;
            if (!string.IsNullOrEmpty(videoData.IndexVideoId))
            {
                var i = 0;
                var completed = false;
                while (i < MAX_RETRY &&
                        !completed)
                    try
                    {
                        i++;
                        videoIndexDto = await ethernaUserClients.IndexClient.VideosClient.VideosGetAsync(videoData.IndexVideoId).ConfigureAwait(false);
                        completed = true;
                    }
                    catch { }
                if (!completed)
                    throw new InvalidOperationException($"Some error during get index video status");
            }

            if (videoIndexDto is not null)
            {
                // Update manifest index.
                Console.WriteLine($"Update Index: {videoData!.IndexVideoId}\t{hashReferenceMetadata}");
                var i = 0;
                var completed = false;
                while (i < MAX_RETRY &&
                        !completed)
                    try
                    {
                        i++;
                        await ethernaUserClients.IndexClient.VideosClient.VideosPutAsync(videoData.IndexVideoId!, hashReferenceMetadata).ConfigureAwait(false);
                        completed = true;
                    }
                    catch { await Task.Delay(3500).ConfigureAwait(false); }
                if (!completed)
                    throw new InvalidOperationException($"Some error during update index video");

                return videoData.IndexVideoId!;
            }
            else
            {
                // Create new manifest index.
                Console.WriteLine($"Create Index: {hashReferenceMetadata}");
                var i = 0;
                var completed = false;
                var indexVideoId = "";
                while (i < MAX_RETRY &&
                        !completed)
                    try
                    {
                        var videoCreateInput = new VideoCreateInput
                        {
                            ManifestHash = hashReferenceMetadata,
                        };
                        i++;
                        indexVideoId = await ethernaUserClients.IndexClient.VideosClient.VideosPostAsync(videoCreateInput).ConfigureAwait(false);
                        completed = true;
                    }
                    catch { }
                if (!completed)
                    throw new InvalidOperationException($"Some error during create index video");

                videoData.SetEthernaIndex(indexVideoId);

                return indexVideoId;
            }
        }

        public async Task<string> CreateBatchAsync()
        {
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    var chainState = await ethernaUserClients.GatewayClient.SystemClient.ChainstateAsync().ConfigureAwait(false);
                    var amount = (long)BATCH_DURANTION_TIME.TotalSeconds * BLOCK_TIME / chainState.CurrentPrice;
                    return await ethernaUserClients.GatewayClient.UsersClient.BatchesPostAsync(BATCH_DEEP, amount).ConfigureAwait(false);
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during create batch");
        }

        public async Task DeleteIndexVideoAsync(string videoId)
        {
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    await ethernaUserClients.IndexClient.VideosClient.VideosDeleteAsync(videoId).ConfigureAwait(false);
                    return;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during delete video");
        }

        public async Task<IEnumerable<VideoDto>> GetAllUserVideoAsync(string userAddress)
        {
            var elements = new List<VideoDto>();
            var i = 0;
            var currentPage = 0;
            const int MaxForPage = 100;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    var result = await ethernaUserClients.IndexClient.UsersClient.Videos2Async(userAddress, currentPage, MaxForPage).ConfigureAwait(false);
                    currentPage++;

                    elements.AddRange(result.Elements);
                    if ((MaxForPage * currentPage) >= result.TotalElements)
                        return elements;
                    i = 0;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    await Task.Delay(3500).ConfigureAwait(false);
                }
            throw new InvalidOperationException($"Some error during get user video");
        }

        public async Task<VideoManifestDto?> GetLastValidManifestAsync(string? videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                return null;

            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    var videoDto = await ethernaUserClients.IndexClient.VideosClient.ManifestAsync(videoId).ConfigureAwait(false);
                    return videoDto?.LastValidManifest;
                }
                catch (IndexApiException ex) when (ex.StatusCode == 404)
                {
                    return null;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during get index video");
        }

        public async Task<SystemParametersDto> GetInfoAsync()
        {
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    return await ethernaUserClients.IndexClient.SystemClient.ParametersAsync().ConfigureAwait(false);
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during get params index");
        }

        public async Task<string> GetBatchIdFromBatchReferenceAsync(string referenceId)
        {
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    return await ethernaUserClients.GatewayClient.SystemClient.PostageBatchRefAsync(referenceId).ConfigureAwait(false);
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during get batch id");
        }

        public async Task<bool> IsBatchUsableAsync(string batchId)
        {
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    return (await ethernaUserClients.GatewayClient.UsersClient.BatchesGetAsync(batchId).ConfigureAwait(false)).Usable;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during get batch status");
        }

        public async Task OfferResourceAsync(string hash)
        {
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    await ethernaUserClients.GatewayClient.ResourcesClient.OffersPostAsync(hash).ConfigureAwait(false);
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during set reference offer");
        }
    }
}
