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
        private const int MAX_RETRY = 3;

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
        public async Task StartUploadAsync(
            VideoUploadData videoUploadData,
            bool pinVideo)
        {
            if (videoUploadData?.VideoUploadDataItems is null ||
                videoUploadData.VideoUploadDataItems.Count <= 0)
                return;

            // Create new batch.
            Console.WriteLine("Create batch...");
            // Create batch.
            var batchReferenceId = await CreateBatchIdFromReferenceAsync().ConfigureAwait(false);
            string batchId;

            // Check and wait until created batchId is avaiable.
            Console.WriteLine("Waiting for batch ready...");
            int timeWaited = 0;
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

            // Upload thumbnail only one time.
            var thumbnailReference = await UploadThumbnailAsync(pinVideo, videoUploadData, batchId).ConfigureAwait(false);
            if (offerVideo)
                await OfferResourceAsync(thumbnailReference).ConfigureAwait(false);

            foreach (var videoUploadItem in videoUploadData.VideoUploadDataItems)
            {
                // Upload video.
                videoUploadItem.UploadedVideoReference = await UploadFileVideoAsync(pinVideo, videoUploadItem, batchId).ConfigureAwait(false);
                await OfferResourceAsync(videoUploadItem.UploadedVideoReference).ConfigureAwait(false);

                // Remove downloaded files.
                if (File.Exists(videoUploadItem.DownloadedFilePath))
                    File.Delete(videoUploadItem.DownloadedFilePath);
                if (File.Exists(videoUploadItem.DownloadedThumbnailPath))
                    File.Delete(videoUploadItem.DownloadedThumbnailPath);
            }

            // Upload metadata.
            var hashMetadataReference = await UploadMetadataAsync(
                videoUploadData,
                batchId,
                thumbnailReference,
                pinVideo).ConfigureAwait(false);
            if (offerVideo)
                await OfferResourceAsync(hashMetadataReference).ConfigureAwait(false);

            // Sync Index.
            Console.WriteLine("Video indexing in progress...");
            await indexerService.IndexManifestAsync(
                hashMetadataReference,
                videoUploadData.MDFileData)
                .ConfigureAwait(false);
        }

        public async Task<string> UploadMetadataAsync(
            MetadataVideo metadataVideo,
            MDFileData mdFileData,
            bool swarmPin)
        {
            var tmpMetadata = Path.GetTempFileName();
            var hashMetadataReference = "";
            try
            {
                await File.WriteAllTextAsync(tmpMetadata, JsonUtility.ToJson(metadataVideo)).ConfigureAwait(false);

                // Upload file.
                var fileParameterInput = new FileParameterInput(
                    File.OpenRead(tmpMetadata),
                    Path.GetFileName("metadata.json"),
                    MimeTypes.GetMimeType("application/json"));

                var i = 0;
                while (i < MAX_RETRY &&
                    string.IsNullOrWhiteSpace(hashMetadataReference))
                    try
                    {
                        i++;
                        using var fileStream = File.OpenRead(tmpMetadata);
                        hashMetadataReference = await beeNodeClient.GatewayClient!.UploadFileAsync(
                            metadataVideo.BatchId!,
                            files: new List<FileParameterInput> { fileParameterInput },
                            swarmPin: swarmPin).ConfigureAwait(false);
                    }
                    catch { }
                if (string.IsNullOrWhiteSpace(hashMetadataReference))
                    throw new InvalidOperationException("Some error during upload of metadata");

                mdFileData.SetEthernaPermalink(hashMetadataReference);
            }
            finally
            {
                if (File.Exists(tmpMetadata))
                    File.Delete(tmpMetadata);
            }

            return hashMetadataReference;
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

            var isSuccessStatusCode = false;
            var i = 0;
            while (i < MAX_RETRY &&
                    !isSuccessStatusCode)
                try
                {
                    i++;
                    var httpResponse = await httpClient.PostAsync(new Uri(gatewayUrl + urlOffer), httpContent).ConfigureAwait(false);
                    isSuccessStatusCode = httpResponse.IsSuccessStatusCode;
                }
                catch { }
            if (!isSuccessStatusCode)
                throw new InvalidProgramException($"Error during offer resource");

            return true;
        }
        
        private async Task<string> UploadFileVideoAsync(
            bool pinVideo, 
            VideoUploadDataItem videoUploadDataItem, 
            string batchId)
        {
            Console.WriteLine($"Uploading video {videoUploadDataItem.Resolution} in progress...");
            var fileParameterInput = new FileParameterInput(
                File.OpenRead(videoUploadDataItem.DownloadedFilePath!),
                Path.GetFileName(videoUploadDataItem.DownloadedFilePath!),
                MimeTypes.GetMimeType(Path.GetFileName(videoUploadDataItem.DownloadedFilePath!)));
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    return await beeNodeClient.GatewayClient!.UploadFileAsync(
                        batchId,
                        files: new List<FileParameterInput> { fileParameterInput },
                        swarmPin: pinVideo).ConfigureAwait(false);
                }
                catch (Exception ex) 
                { 
                    Console.WriteLine(ex.StackTrace); 
                }
            throw new InvalidOperationException("Some error during upload of video");
        }

        private async Task<string> UploadMetadataAsync(
            VideoUploadData videoUploadData,
            string batchId,
            string thumbnailReference,
            bool swarmPin)
        {
            if (videoUploadData is null)
                throw new ArgumentNullException(nameof(videoUploadData));
            if (string.IsNullOrWhiteSpace(videoUploadData.MDFileData.Title))
                throw new InvalidOperationException("Title not defined");
            if (string.IsNullOrWhiteSpace(videoUploadData.MDFileData.Description))
                throw new InvalidOperationException("Description not defined");

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
                videoUploadData.MDFileData.Duration,
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                videoUploadData.OriginalQuality,
                userEthAddr,
                videoUploadData.VideoUploadDataItems.Select(video => new MetadataVideoSource(video.Bitrate, video.Resolution + "p", video.UploadedVideoReference!, video.Size)),
                swarmImageRaw,
                videoUploadData.MDFileData.Title,
                null,
                "1.1",
                new MetadataPersonalData { Mode = "importer", VideoId = videoUploadData.MDFileData.YoutubeId! });

            return await UploadMetadataAsync(metadataVideo, videoUploadData.MDFileData, swarmPin).ConfigureAwait(false);
        }

        private async Task<string> UploadThumbnailAsync(bool pinVideo, VideoUploadData videoUpload, string batchId)
        {
            Console.WriteLine("Uploading thumbnail in progress...");
            var fileThumbnailParameterInput = new FileParameterInput(
                File.OpenRead(videoUpload.DownloadedThumbnailPath!),
                Path.GetFileName(videoUpload.DownloadedThumbnailPath!),
                MimeTypes.GetMimeType(Path.GetFileName(videoUpload.DownloadedThumbnailPath!)));

            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    return await beeNodeClient.GatewayClient!.UploadFileAsync(
                        batchId,
                        files: new List<FileParameterInput> { fileThumbnailParameterInput },
                        swarmPin: pinVideo).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.StackTrace);
                }
            throw new InvalidOperationException("Some error during upload of thumbnail");
        }
    }
}
