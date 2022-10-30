using Etherna.BeeNet;
using Etherna.BeeNet.InputModels;
using Etherna.DevconArchiveVideoParser.CommonData.Json;
using Etherna.DevconArchiveVideoParser.CommonData.Models;
using Etherna.DevconArchiveVideoParser.CommonData.Responses;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoParser.Services
{
    internal class VideoUploaderService
    {
        // Privates.
        private readonly BeeNodeClient beeNodeClient;
        private readonly HttpClient httpClient;
        private readonly IndexerService indexerService;
        private readonly string gatewayUrl;
        private readonly bool offerVideo;
        private readonly string userEthAddr;


        // Const.
        private const int BATCH_DEEP = 20;
        private const int BATCH_DURANTION_TIME = 31536000;
        private const int BATCH_WAITING_TIME = 7 * 1000;
        private const int BATCH_TIMEOUT_TIME = 5 * 60 * 1000;
        private const int BLOCK_TIME = 5;
        private const string GATEWAY_API_CREATEBATCH = "api/v0.3/users/current/batches";
        private const string GATEWAY_API_CHAINSTATE = "api/v0.3/system/chainstate";
        private const string GATEWAY_API_GETBATCH = "api/v0.3/users/current/batches";
        private const string GATEWAY_API_GETBATCH_REFERENCE = "api/v0.3/System/postageBatchRef/";
        private const string GATEWAY_API_OFFER_RESOURCE = "api/v0.3/Resources/{0}/offers";

        // Constractor.
        public VideoUploaderService(
            HttpClient httpClient,
            BeeNodeClient beeNodeClient,
            IndexerService indexerService,
            string gatewayUrl,
            string userEthAddr,
            bool offerVideo)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (indexerService is null)
                throw new ArgumentNullException(nameof(indexerService));
            if (gatewayUrl is null)
                throw new ArgumentNullException(nameof(gatewayUrl));

            this.beeNodeClient = beeNodeClient;
            this.httpClient = httpClient;
            this.indexerService = indexerService;
            this.gatewayUrl = gatewayUrl;
            this.userEthAddr = userEthAddr;
            this.offerVideo = offerVideo;
        }

        // Public methods.
        public async Task<MDFileData> StartUploadAsync(
            List<VideoUploadData> videoUploadDatas,
            bool pinVideo)
        {
            var thumbnailUploaded = false;
            foreach (var videoUpload in videoUploadDatas)
            {
                // Create new batch.
                Console.WriteLine("Create batch...");
                // Create batch.
                var batchReferenceId = await CreateBatchIdFromReferenceAsync().ConfigureAwait(false);

                // Check and wait until created batchId is avaiable.
                Console.WriteLine("Waiting for batch ready...");
                int timeWaited = 0;
                string batchId = "";
                do
                {
                    // Timeout throw exception.
                    if (timeWaited >= BATCH_TIMEOUT_TIME)
                    {
                        var ex = new InvalidOperationException("Batch not avaiable");
                        ex.Data.Add("BatchReferenceId", batchReferenceId);
                        throw ex;
                    }

                    // Waiting for batchId avaiable.
                    await Task.Delay(BATCH_WAITING_TIME).ConfigureAwait(false);
                    batchId = await GetBatchIdFromReference(batchReferenceId).ConfigureAwait(false);
                    timeWaited += BATCH_WAITING_TIME;
                } while (string.IsNullOrWhiteSpace(batchId));

                // Check and wait until created batch is usable.
                timeWaited = 0;
                bool batchUsable;
                do
                {
                    // Timeout throw exception.
                    if (timeWaited >= BATCH_TIMEOUT_TIME)
                    {
                        var ex = new InvalidOperationException("Batch not usable");
                        ex.Data.Add("BatchId", batchId);
                        throw ex;
                    }

                    // Waiting for batch ready.
                    await Task.Delay(BATCH_WAITING_TIME).ConfigureAwait(false);
                    batchUsable = await GetBatchUsableAsync(batchId).ConfigureAwait(false);
                    timeWaited += BATCH_WAITING_TIME;
                } while (!batchUsable);

                // Upload file.
                Console.WriteLine("Uploading video in progress...");
                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(videoUpload.DownloadedFilePath!),
                    Path.GetFileName(videoUpload.DownloadedFilePath!),
                    MimeTypes.GetMimeType(Path.GetFileName(videoUpload.DownloadedFilePath!)));

                var videoReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                    batchId,
                    files: new List<FileParameterInput> { fileParameterInput },
                    swarmPin: pinVideo).ConfigureAwait(false);

                var thumbnailReference = "";
                if (!thumbnailUploaded)
                {
                    // Upload thumbnail only one time.
                    Console.WriteLine("Uploading thumbnail in progress...");
                    var fileThumbnailParameterInput = new FileParameterInput(
                        File.OpenRead(videoUpload.DownloadedThumbnailPath!),
                        Path.GetFileName(videoUpload.DownloadedThumbnailPath!),
                        MimeTypes.GetMimeType(Path.GetFileName(videoUpload.DownloadedThumbnailPath!)));

                    thumbnailReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                        batchId,
                        files: new List<FileParameterInput> { fileThumbnailParameterInput },
                        swarmPin: pinVideo).ConfigureAwait(false);

                    thumbnailUploaded = true;
                }

                // Upload metadata.
                var hashMetadataReference = await UploadMetadataAsync(
                    videoUpload,
                    videoUploadDatas.First().MDFileData,
                    batchId, 
                    thumbnailReference,
                    videoReference,
                    pinVideo).ConfigureAwait(false);

                if (offerVideo)
                {
                    // Offer all uploaded content.
                    Console.WriteLine("Flag video by offer from creator in progress...");
                    await OfferResourceAsync(videoReference).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(thumbnailReference) && !thumbnailUploaded)
                        await OfferResourceAsync(thumbnailReference).ConfigureAwait(false);
                    await OfferResourceAsync(hashMetadataReference).ConfigureAwait(false);
                }

                // Sync Index.
                Console.WriteLine("Video indexing in progress...");
                var indexVideoId = await indexerService.IndexManifestAsync(
                    hashMetadataReference,
                    videoUpload.MDFileData)
                    .ConfigureAwait(false);

                // Remove downloaded files.
                if (File.Exists(videoUpload.DownloadedFilePath))
                    File.Delete(videoUpload.DownloadedFilePath);
                if (File.Exists(videoUpload.DownloadedThumbnailPath))
                    File.Delete(videoUpload.DownloadedThumbnailPath);
            }

            return videoUploadDatas.First().MDFileData;
        }

        public async Task<string> UploadMetadataAsync(
            MetadataVideo metadataVideo,
            MDFileData mdFileData,
            bool swarmPin)
        {
            var tmpMetadata = Path.GetTempFileName();
            try
            {
                await File.WriteAllTextAsync(tmpMetadata, JsonUtility.ToJson(metadataVideo)).ConfigureAwait(false);

                // Upload file.
                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(tmpMetadata),
                    Path.GetFileName("metadata.json"),
                    MimeTypes.GetMimeType("application/json"));

                using var fileStream = File.OpenRead(tmpMetadata);
                var hashMetadataReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                    metadataVideo.BatchId!,
                    files: new List<FileParameterInput> { fileParameterInput },
                    swarmPin: swarmPin).ConfigureAwait(false);

                mdFileData.SetEthernaPermanent(hashMetadataReference);

                return hashMetadataReference;
            }
            finally
            {
                if (File.Exists(tmpMetadata))
                    File.Delete(tmpMetadata);
            }
        }

        // Private methods.
        private async Task<string> CreateBatchIdFromReferenceAsync()
        {
            var httpResponse = await httpClient.GetAsync(new Uri(gatewayUrl + GATEWAY_API_CHAINSTATE)).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var chainPriceDto = JsonUtility.FromJson<ChainPriceResponse>(responseText);
            if (chainPriceDto is null)
                throw new ArgumentNullException("Chainstate result is null");

            var amount = (long)BATCH_DURANTION_TIME * BLOCK_TIME / chainPriceDto.CurrentPrice;
            using var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
            httpResponse = await httpClient.PostAsync(new Uri(gatewayUrl + GATEWAY_API_CREATEBATCH + $"?depth={BATCH_DEEP}&amount={amount}"), httpContent).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<bool> GetBatchUsableAsync(string batchId)
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"{gatewayUrl}{GATEWAY_API_GETBATCH}/{batchId}")).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
                return false;

            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var postageBatch = JsonUtility.FromJson<BatchMinimalInfoResponse>(responseText);

            return postageBatch?.Usable ?? false;
        }

        private async Task<string> GetBatchIdFromReference(string referenceId)
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"{gatewayUrl}{GATEWAY_API_GETBATCH_REFERENCE}/{referenceId}")).ConfigureAwait(false);

            if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                return "";

            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }

        private async Task<bool?> OfferResourceAsync(string reference)
        {
            var urlOffer = string.Format(CultureInfo.InvariantCulture, GATEWAY_API_OFFER_RESOURCE, reference);
            using var httpContent = new StringContent("{}", Encoding.UTF8, "application/json");
            var httpResponse = await httpClient.PostAsync(new Uri(gatewayUrl + urlOffer), httpContent).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
                throw new InvalidProgramException($"Error during offer resource");

            return true;
        }

        private async Task<string> UploadMetadataAsync(
            VideoUploadData videoUploadData,
            MDFileData mdFileData,
            string batchId,
            string thumbnailReference,
            string videoReference,
            bool swarmPin)
        {
            if (videoUploadData is null)
                throw new ArgumentNullException(nameof(videoUploadData));
            if (string.IsNullOrWhiteSpace(videoUploadData.MDFileData.Title))
                throw new InvalidOperationException("Title not defined");
            if (string.IsNullOrWhiteSpace(videoUploadData.MDFileData.Description))
                throw new InvalidOperationException("Description not defined");
            if (string.IsNullOrWhiteSpace(videoUploadData.Quality))
                throw new InvalidOperationException("Quality not defined");
            
            SwarmImageRaw? swarmImageRaw = null;
            if (!string.IsNullOrWhiteSpace(thumbnailReference) &&
                !string.IsNullOrWhiteSpace(videoUploadData.DownloadedThumbnailPath))
            {
                using var input = File.OpenRead(videoUploadData.DownloadedThumbnailPath);
                using var inputStream = new SKManagedStream(input);
                using var sourceImage = SKBitmap.Decode(inputStream);
                var hash = Blurhash.SkiaSharp.Blurhasher.Encode(sourceImage, 4, 4);
                swarmImageRaw = new SwarmImageRaw(
                    sourceImage.Width / sourceImage.Height,
                    hash,
                    new Dictionary<string, string> { { $"{sourceImage.Width}w", thumbnailReference } },
                    "1.0");
            }

            var metadataVideo = new MetadataVideo(
                batchId,
                videoUploadData.MDFileData.Description,
                videoUploadData.Duration,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                videoUploadData.Quality,
                userEthAddr,
                new List<MetadataVideoSource> { new MetadataVideoSource(videoUploadData.Bitrate, videoUploadData.Quality, videoReference, videoUploadData.Size) },
                swarmImageRaw,
                videoUploadData.MDFileData.Title,
                null,
                "1.1",
                new MetadataExtraInfo { Mode = "importer", VideoId = mdFileData.YoutubeId! });

            return await UploadMetadataAsync(metadataVideo, mdFileData, swarmPin).ConfigureAwait(false);
        }

    }
}
