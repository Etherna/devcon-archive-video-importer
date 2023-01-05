using Etherna.BeeNet;
using Etherna.BeeNet.InputModels;
using Etherna.DevconArchiveVideoImporter.Dtos;
using Etherna.DevconArchiveVideoImporter.Models;
using Etherna.DevconArchiveVideoImporter.Utilities;
using Etherna.ServicesClient.Clients.Index;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using YoutubeExplode.Common;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    internal class VideoUploaderService : IVideoUploaderService
    {
        // Fields.
        private readonly BeeNodeClient beeNodeClient;
        private readonly EthernaUserClientsAdapter ethernaClientService;
        private readonly string userEthAddr;
        private readonly HttpClient httpClient;


        // Const.
        private readonly TimeSpan BATCH_CHECK_TIME = new(0, 0, 0, 10);
        private readonly TimeSpan BATCH_TIMEOUT_TIME = new(0, 0, 7, 0);
        private const int MAX_RETRY = 3;

        // Constractor.
        public VideoUploaderService(
            BeeNodeClient beeNodeClient,
            EthernaUserClientsAdapter ethernaClientService,
            string userEthAddr,
            HttpClient httpClient)
        {
            if (beeNodeClient is null)
                throw new ArgumentNullException(nameof(beeNodeClient));
            if (ethernaClientService is null)
                throw new ArgumentNullException(nameof(ethernaClientService));

            this.beeNodeClient = beeNodeClient;
            this.ethernaClientService = ethernaClientService;
            this.userEthAddr = userEthAddr;
            this.httpClient = httpClient;
        }

        // Public methods.
        public async Task UploadVideoAsync(
            VideoData videoData,
            bool pinVideo,
            bool offerVideo)
        {
            if (videoData?.VideoDataResolutions is null ||
                videoData.VideoDataResolutions.Count <= 0)
                return;

            // Create new batch.
            Console.WriteLine("Create batch...");

            //var batchReferenceId = await ethernaClientService.CreateBatchAsync().ConfigureAwait(false);
            var batchReferenceId = await CreateBatchAsync().ConfigureAwait(false);
#pragma warning disable CA1307 // Specify StringComparison for clarity
            batchReferenceId = batchReferenceId.Replace("\"", "");
#pragma warning restore CA1307 // Specify StringComparison for clarity

            // Check and wait until created batchId is avaiable.
            Console.WriteLine("Waiting for batch ready...");

            double timeWaited = 0;
            string batchId;
            do
            {
                // Timeout throw exception.
                if (timeWaited >= BATCH_TIMEOUT_TIME.TotalSeconds)
                {
                    var ex = new InvalidOperationException("Batch not avaiable");
                    ex.Data.Add("BatchReferenceId", batchReferenceId);
                    throw ex;
                }

                // Waiting for batchId avaiable.
                await Task.Delay((int)BATCH_CHECK_TIME.TotalMilliseconds).ConfigureAwait(false);
                //batchId = await ethernaClientService.GetBatchIdFromBatchReferenceAsync(batchReferenceId).ConfigureAwait(false);
                batchId = await GetBatchIdFromReference(batchReferenceId).ConfigureAwait(false);
                timeWaited += BATCH_CHECK_TIME.TotalSeconds;
            } while (string.IsNullOrWhiteSpace(batchId));

            // Check and wait until created batch is usable.
            timeWaited = 0;
            bool batchUsable;
            do
            {
                // Timeout throw exception.
                if (timeWaited >= BATCH_TIMEOUT_TIME.TotalSeconds)
                {
                    var ex = new InvalidOperationException("Batch not usable");
                    ex.Data.Add("BatchId", batchId);
                    throw ex;
                }

                // Waiting for batch ready.
                await Task.Delay((int)BATCH_CHECK_TIME.TotalMilliseconds).ConfigureAwait(false);
                //batchUsable = await ethernaClientService.IsBatchUsableAsync(batchId).ConfigureAwait(false);
                batchUsable = await GetBatchAsync(batchId).ConfigureAwait(false);
                timeWaited += BATCH_CHECK_TIME.TotalSeconds;
            } while (!batchUsable);

            // Upload thumbnail only one time.
            var thumbnailReference = await UploadThumbnailAsync(pinVideo, videoData, batchId).ConfigureAwait(false);
            if (offerVideo)
                await ethernaClientService.OfferResourceAsync(thumbnailReference).ConfigureAwait(false);

            foreach (var specificVideoResolution in videoData.VideoDataResolutions)
            {
                // Upload video.
                var videoReference = await UploadFileVideoAsync(pinVideo, specificVideoResolution, batchId).ConfigureAwait(false);
                specificVideoResolution.SetUploadedVideoReference(videoReference);
                if (offerVideo)
                    await ethernaClientService.OfferResourceAsync(specificVideoResolution.UploadedVideoReference!).ConfigureAwait(false);
            }

            // Upload metadata.
            var hashMetadataReference = await UploadMetadataAsync(
                videoData,
                batchId,
                thumbnailReference,
                pinVideo).ConfigureAwait(false);
            if (offerVideo)
                await ethernaClientService.OfferResourceAsync(hashMetadataReference).ConfigureAwait(false);

            // Sync Index.
            Console.WriteLine("Video indexing in progress...");
            await ethernaClientService.UpsertManifestToIndex(
                hashMetadataReference,
                videoData)
                .ConfigureAwait(false);
        }

        public async Task<string> UploadMetadataAsync(
            VideoManifestDto videoManifestDto,
            VideoData videoData,
            bool swarmPin)
        {
            var metadataManifestInsertInput = new MetadataManifestInsertInput(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                userEthAddr,
                videoManifestDto.BatchId,
                videoData.Description,
                videoData.Duration,
                 $"{videoData.VideoDataResolutions.First().Resolution}",
                 JsonUtility.ToJson(new MetadataPersonalDataDto { Mode = MetadataUploadMode.DevconImporter, VideoId = videoData.YoutubeId! }),
                 new MetadataImageInput(
                     videoManifestDto.Thumbnail.AspectRatio,
                     videoManifestDto.Thumbnail.Blurhash,
                     videoManifestDto.Thumbnail.Sources),
                 videoData.Title);

            return await UploadMetadataAsync(
                metadataManifestInsertInput,
                videoData,
                swarmPin).ConfigureAwait(false);
        }

        public async Task<string> UploadMetadataAsync(
            MetadataManifestInsertInput videoManifestDto,
            VideoData videoData,
            bool swarmPin)
        {
            var tmpMetadata = Path.GetTempFileName();
            var hashMetadataReference = "";
            try
            {
                await File.WriteAllTextAsync(tmpMetadata, JsonUtility.ToJson(videoManifestDto)).ConfigureAwait(false);

                // Upload file.
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
                            videoManifestDto.BatchId!,
                            files: new List<FileParameterInput> { fileParameterInput },
                            swarmPin: swarmPin).ConfigureAwait(false);
                    }
                    catch { await Task.Delay(3500).ConfigureAwait(false); }
                if (string.IsNullOrWhiteSpace(hashMetadataReference))
                    throw new InvalidOperationException("Some error during upload of metadata");

                videoData.SetEthernaPermalink(hashMetadataReference);
            }
            finally
            {
                if (File.Exists(tmpMetadata))
                    File.Delete(tmpMetadata);
            }

            return hashMetadataReference;
        }

        // Private methods.
        static readonly JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };
        private async Task<string> CreateBatchAsync()
        {
            var httpResponse = await httpClient.GetAsync(new Uri("https://gateway.etherna.io/api/v0.3/system/chainstate")).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var chainPriceDto = System.Text.Json.JsonSerializer.Deserialize<ChainPriceDto>(responseText, options);
            if (chainPriceDto is null)
                throw new ArgumentNullException("Chainstate result is null");

            var amount = (long)31536000 * 5 / chainPriceDto.CurrentPrice;
            using var httpContent = new System.Net.Http.StringContent("{}", System.Text.Encoding.UTF8, "application/json");
            httpResponse = await httpClient.PostAsync(new Uri($"https://gateway.etherna.io/api/v0.3/users/current/batches?depth={20}&amount={amount}"), httpContent).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();
            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        private async Task<string> GetBatchIdFromReference(string referenceId)
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"https://gateway.etherna.io/api/v0.3/System/postageBatchRef/{referenceId}")).ConfigureAwait(false);

            if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                return "";

            return await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
        }
        private async Task<bool> GetBatchAsync(string batchId)
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"https://gateway.etherna.io/api/v0.3/users/current/batches/{batchId}")).ConfigureAwait(false);

            if (!httpResponse.IsSuccessStatusCode)
                return false;

            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<BatchMinimalInfoDto>(responseText, options)?.Usable ?? false;
        }

        private async Task<string> UploadFileVideoAsync(
            bool pinVideo,
            VideoDataResolution videoUploadDataItem,
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
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException("Some error during upload of video");
        }

        private async Task<string> UploadMetadataAsync(
            VideoData videoData,
            string batchId,
            string thumbnailReference,
            bool swarmPin)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));
            if (string.IsNullOrWhiteSpace(videoData.Title))
                throw new InvalidOperationException("Title not defined");
            if (string.IsNullOrWhiteSpace(videoData.Description))
                throw new InvalidOperationException("Description not defined");

            // Thumbnail.
            MetadataImageInput? swarmImageRaw = null;
            if (!string.IsNullOrWhiteSpace(videoData.DownloadedThumbnailPath))
            {
                using var input = File.OpenRead(videoData.DownloadedThumbnailPath);
                using var inputStream = new SKManagedStream(input);
                using var sourceImage = SKBitmap.Decode(inputStream);
                var hash = Blurhash.SkiaSharp.Blurhasher.Encode(sourceImage, 4, 4);
                swarmImageRaw = new MetadataImageInput(
                    sourceImage.Width / sourceImage.Height,
                    hash,
                    new Dictionary<string, string> { { $"{sourceImage.Width}w", thumbnailReference } });
            }

            // Manifest.
            var metadataVideo = new MetadataManifestInsertInput(
                DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                userEthAddr,
                batchId,
                videoData.Description,
                videoData.Duration,
                 $"{videoData.VideoDataResolutions.First().Resolution}",
                 JsonUtility.ToJson(new MetadataPersonalDataDto { Mode = MetadataUploadMode.DevconImporter, VideoId = videoData.YoutubeId! }),
                 swarmImageRaw,
                 videoData.Title);

            foreach (var video in videoData.VideoDataResolutions)
                metadataVideo.Sources.Add(new SourceDto
                {
                    Bitrate = video.Bitrate,
                    Quality = video.Resolution,
                    Reference = video.UploadedVideoReference!,
                    Size = video.Size
                });

            return await UploadMetadataAsync(metadataVideo, videoData, swarmPin).ConfigureAwait(false);
        }
        private async Task<string> UploadThumbnailAsync(
            bool pinVideo,
            VideoData videoData,
            string batchId)
        {
            Console.WriteLine("Uploading thumbnail in progress...");
            var fileThumbnailParameterInput = new FileParameterInput(
                File.OpenRead(videoData.DownloadedThumbnailPath!),
                Path.GetFileName(videoData.DownloadedThumbnailPath!),
                MimeTypes.GetMimeType(Path.GetFileName(videoData.DownloadedThumbnailPath!)));

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
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException("Some error during upload of thumbnail");
        }
    }

    internal class ChainPriceDto
    {
        public long CurrentPrice { get; set; }
    }
    internal class BatchMinimalInfoDto
    {
        public string? Id { get; set; }
        public bool Exists { get; set; }
        public bool Usable { get; set; }
    }
}
