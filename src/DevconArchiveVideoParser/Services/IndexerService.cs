using Etherna.DevconArchiveVideoImporter.Index.Models;
using Etherna.DevconArchiveVideoImporter.Json;
using Etherna.DevconArchiveVideoImporter.Responses;
using Etherna.DevconArchiveVideoParser.CommonData.Requests;
using Etherna.DevconArchiveVideoParser.Models;
using System;
using System.Globalization;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    public class IndexerService
    {
        private readonly HttpClient httpClient;
        private readonly string indexUrl;
        private const string ETHERNA_INDEX_PARAMS_INFO = "api/v0.3/System/parameters";
        private const string INDEX_API_CREATEBATCH = "api/v0.3/videos";
        private const string INDEX_API_MANIFEST = "api/v0.3/videos/{0}";
        private const int MAX_RETRY = 3;

        public IndexerService(
            HttpClient httpClient,
            string indexUrl)
        {
            this.httpClient = httpClient;
            this.indexUrl = indexUrl;
        }

        // Methods.
        public async Task<string> IndexManifestAsync(
            string hashReferenceMetadata,
            VideoData videoData)
        {
            //TODO Insert retry for http error.
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));

            var haveIndexLink = false;
            if (!string.IsNullOrWhiteSpace(videoData.IndexVideoId))
            {
                var i = 0;
                var completed = false;
                while (i < MAX_RETRY &&
                        !completed)
                    try
                    {
                        i++;
                        var httpGetResponse = await httpClient.GetAsync(new Uri(indexUrl + INDEX_API_CREATEBATCH + $"/{videoData.IndexVideoId}")).ConfigureAwait(false);
                        haveIndexLink = httpGetResponse.StatusCode == System.Net.HttpStatusCode.OK;
                        completed = true;
                    }
                    catch { }
                if (!completed)
                    throw new InvalidOperationException($"Some error during get index video status");
            }

            HttpResponseMessage httpResponse;
            if (haveIndexLink)
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
                        using var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
                        httpResponse = await httpClient.PutAsync(new Uri(indexUrl + INDEX_API_CREATEBATCH + $"/{videoData!.IndexVideoId}?newHash={hashReferenceMetadata}"), httpContent).ConfigureAwait(false);
                        httpResponse.EnsureSuccessStatusCode();
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
                        i++;
                        var indexManifestRequest = new IndexManifestRequest(hashReferenceMetadata);
                        using var httpContent = new StringContent(JsonUtility.ToJson(indexManifestRequest), Encoding.UTF8, "application/json");
                        httpResponse = await httpClient.PostAsync(new Uri(indexUrl + INDEX_API_CREATEBATCH), httpContent).ConfigureAwait(false);
                        httpResponse.EnsureSuccessStatusCode();
                        indexVideoId = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        completed = true;
                    }
                    catch { }
                if (!completed)
                    throw new InvalidOperationException($"Some error during create index video");

                videoData.SetEthernaIndex(indexVideoId);

                return indexVideoId;
            }
        }

        public async Task<MetadataVideo?> GetLastValidManifestAsync(string? videoId)
        {
            if (string.IsNullOrWhiteSpace(videoId))
                return null;

            var manifestApi = string.Format(CultureInfo.InvariantCulture, INDEX_API_MANIFEST, videoId);
            var httpResponse = await httpClient.GetAsync(new Uri($"{indexUrl}{manifestApi}")).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();

            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonUtility.FromJson<VideoIndexResponse?>(responseText)?.LastValidManifest;
        }

        public async Task<IndexParamsResponse> GetParamsInfoAsync()
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"{indexUrl}{ETHERNA_INDEX_PARAMS_INFO}")).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();

            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonUtility.FromJson<IndexParamsResponse>(responseText) ?? new IndexParamsResponse();
        }

    }
}
