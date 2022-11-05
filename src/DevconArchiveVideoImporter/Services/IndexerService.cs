using Etherna.DevconArchiveVideoParser.Models;
using Etherna.ServicesClient;
using Etherna.ServicesClient.Clients.Index;
using System;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    public class IndexerService
    {
        private readonly EthernaUserClients ethernaUserClients;
        private const int MAX_RETRY = 3;

        public IndexerService(EthernaUserClients ethernaUserClients)
        {
            this.ethernaUserClients = ethernaUserClients;
        }

        // Methods.
        public async Task<string> IndexManifestAsync(
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
                    catch { }
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
                catch { }
            throw new InvalidOperationException($"Some error during create index video");
        }

        public async Task<SystemParametersDto> GetParamsInfoAsync()
        {
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    return await ethernaUserClients.IndexClient.SystemClient.ParametersAsync().ConfigureAwait(false);
                }
                catch { }
            throw new InvalidOperationException($"Some error during get params index");
        }

    }
}
