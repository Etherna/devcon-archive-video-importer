using Etherna.DevconArchiveVideoImporter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using VideoLibrary;

namespace Etherna.DevconArchiveVideoImporter.Services
{
#pragma warning disable CA1001 // Types that own disposable fields should be disposable
    public class YoutubeDownloadService : IVideoDownloadService
#pragma warning restore CA1001 // Types that own disposable fields should be disposable
    {
        // Const.
        private readonly long CHUNCK_SINZE = 10_485_760;
        private const int MAX_RETRY = 3;

        // Fields.
        private readonly HttpClient client = new();
        private readonly YouTube youTube = new YouTube();
        
        // Methods.
        public async Task<List<VideoDataResolution>> GetAllResolutionInfoAsync(VideoData videoData)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));
            if (string.IsNullOrWhiteSpace(videoData.YoutubeUrl))
                throw new InvalidOperationException("Invalid youtube url");
            var videos = await youTube.GetAllVideosAsync(videoData.YoutubeUrl).ConfigureAwait(false);

            // Take best resolution with audio.
            var videoWithAudio = videos
                .Where(video => video.AudioBitrate != -1 &&
                                video.Resolution > 0)
                .ToList();
            var allResolutions = videoWithAudio
                .Select(video => video.Resolution)
                .OrderByDescending(res => res)
                .Distinct();

            var sourceVideoInfos = new List<VideoDataResolution>();
            foreach (var currentRes in allResolutions)
            {
                var videoDownload = videoWithAudio
                .First(video => video.Resolution == currentRes);

                var videoUri = new Uri(videoDownload.Uri);
                var fileSize = await GetContentLengthAsync(videoUri).ConfigureAwait(false);

                sourceVideoInfos.Add(new VideoDataResolution(
                    videoDownload.AudioBitrate,
                    $"{videoDownload.Resolution}_{videoDownload.FullName}",
                    videoDownload.Resolution,
                    videoUri));
            }

            return sourceVideoInfos;
        }

        public async Task DownloadVideoAsync(
            Uri uri,
            string filePath,
            IProgress<(long totalBytesCopied, long fileSize)> progress)
        {
            if (uri is null)
                throw new ArgumentNullException(nameof(uri));

            var fileSize = await GetContentLengthAsync(uri).ConfigureAwait(false) ?? 0;
            if (fileSize == 0)
                throw new InvalidOperationException("File has no any content");

            using var output = File.OpenWrite(filePath);
            var segmentCount = (int)Math.Ceiling(1.0 * fileSize / CHUNCK_SINZE);
            var totalBytesCopied = 0L;
            for (var i = 0; i < segmentCount; i++)
            {
                var from = i * CHUNCK_SINZE;
                var to = (i + 1) * CHUNCK_SINZE - 1;
                var request = new HttpRequestMessage(HttpMethod.Get, uri);
                request.Headers.Range = new RangeHeaderValue(from, to);
                using (request)
                {
                    // Download Stream
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                        response.EnsureSuccessStatusCode();
                    var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

                    //File Steam
                    var buffer = new byte[81920];
                    int bytesCopied;
                    do
                    {
                        bytesCopied = await stream.ReadAsync(buffer).ConfigureAwait(false);
                        await output.WriteAsync(buffer.AsMemory(0, bytesCopied)).ConfigureAwait(false);
                        totalBytesCopied += bytesCopied;
                        progress?.Report(new (totalBytesCopied, fileSize));
                    } while (bytesCopied > 0);
                }
            }
        }

        public async Task<string> DownloadThumbnailAsync(string videoId, string tmpFolder)
        {
            string filePath = $"{tmpFolder}/{videoId}.jpg";
            var url = $"https://img.youtube.com/vi/{videoId}/maxresdefault.jpg";
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    using var httpClient = new HttpClient();
                    var streamGot = await httpClient.GetStreamAsync(url).ConfigureAwait(false);
                    using var fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
                    await streamGot.CopyToAsync(fileStream).ConfigureAwait(false);

                    return filePath;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Some error during download of thumbnail {url}");
        }

        // Helpers.
        private async Task<long?> GetContentLengthAsync(Uri requestUri)
        {
            // retry for prevent case of network error.
            var i = 0;
            while (i < MAX_RETRY)
                try
                {
                    i++;
                    using var request = new HttpRequestMessage(HttpMethod.Head, requestUri);
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                    response.EnsureSuccessStatusCode();
                    return response.Content.Headers.ContentLength;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            throw new InvalidOperationException($"Can't get the file size");
        }
    }
}
