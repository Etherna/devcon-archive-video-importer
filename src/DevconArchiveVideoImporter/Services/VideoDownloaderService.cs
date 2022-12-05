using Etherna.DevconArchiveVideoImporter.Models;
using MetadataExtractor;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using VideoLibrary;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    internal class VideoDownloaderService : IVideoDownloaderService, IDisposable
    {
        // Const.
        private readonly long CHUNCK_SINZE = 10_485_760;
        private const int MAX_RETRY = 3;

        // Fields.
        private readonly string tmpFolder;
        private readonly HttpClient client = new();
        private readonly YouTube youTubeClient = new YouTube();

        // Constractor.
        public VideoDownloaderService(string tmpFolder)
        {
            this.tmpFolder = tmpFolder;
        }

        // Public methods.
        public async Task<VideoData> StartDownloadAsync(VideoData videoData)
        {
            if (string.IsNullOrWhiteSpace(videoData?.YoutubeUrl))
                throw new InvalidOperationException("Invalid YoutubeUrl");

            try
            {
                // Take best video resolution.
                var videoResolutions = await GetAllResolutionInfoAsync(videoData).ConfigureAwait(false);
                if (videoResolutions is null ||
                    videoResolutions.Count == 0)
                    throw new InvalidOperationException($"Not found video");

                // Download each video reoslution.
                foreach (var videoInfo in videoResolutions)
                {
                    // Start download and show progress.

                    videoInfo.SetDownloadedFilePath(Path.Combine(tmpFolder, videoInfo.Name));

                    var i = 0;
                    var downloaded = false;
                    while (i < MAX_RETRY &&
                            !downloaded)
                        try
                        {
                            i++;
                            await DownloadVideoAsync(
                                videoInfo.Uri,
                                videoInfo.DownloadedFilePath!,
                                new Progress<(long totalBytesCopied, long fileSize)>((progressStatus) =>
                                {
                                    var percent = (int)(progressStatus.totalBytesCopied * 100 / progressStatus.fileSize);
                                    Console.Write($"Downloading resolution {videoInfo.Resolution}.. ( % {percent} ) {progressStatus.totalBytesCopied / (1024 * 1024)} / {progressStatus.fileSize / (1024 * 1024)} MB\r");
                                })).ConfigureAwait(false);
                            downloaded = true;
                        }
                        catch { await Task.Delay(3500).ConfigureAwait(false); }
                    if (!downloaded)
                        throw new InvalidOperationException($"Some error during download of video {videoInfo.Uri}");
                    Console.WriteLine("");

                    // Set video info from downloaded video.
                    var fileSize = new FileInfo(videoInfo.DownloadedFilePath!).Length;
                    videoInfo.SetVideoInfo(
                        videoInfo.Name,
                        fileSize,
                        GetDuration(videoInfo.DownloadedFilePath));

                    if (videoInfo.Duration <= 0)
                    {
                        throw new InvalidOperationException($"Invalid Duration: {videoInfo.Duration}");
                    }
                }

                // Download Thumbnail.
                var downloadedThumbnailPath = await DownloadThumbnailAsync(videoData.YoutubeId!, tmpFolder).ConfigureAwait(false);
                var originalQuality = videoResolutions.First().Resolution + "p";

                videoData.DownloadedThumbnailPath = downloadedThumbnailPath;
                videoData.VideoDataResolutions = videoResolutions;

                return videoData;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public void Dispose()
        {
            client?.Dispose();
        }

        // Private Methods.
        private async Task<List<VideoDataResolution>> GetAllResolutionInfoAsync(VideoData videoData)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));
            if (string.IsNullOrWhiteSpace(videoData.YoutubeUrl))
                throw new InvalidOperationException("Invalid youtube url");
            var videos = await youTubeClient.GetAllVideosAsync(videoData.YoutubeUrl).ConfigureAwait(false);

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
                request.Headers.Range = new System.Net.Http.Headers.RangeHeaderValue(from, to);
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
                        progress?.Report(new(totalBytesCopied, fileSize));
                    } while (bytesCopied > 0);
                }
            }
        }

        private async Task<string> DownloadThumbnailAsync(string videoId, string tmpFolder)
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

        private static int GetDuration(string? pathToVideoFile)
        {
            if (string.IsNullOrWhiteSpace(pathToVideoFile))
                return 0;

            using var stream = File.OpenRead(pathToVideoFile);
            var directories = ImageMetadataReader.ReadMetadata(stream);
            foreach (var itemDir in directories)
            {
                if (itemDir.Name != "QuickTime Movie Header")
                    continue;
                foreach (var itemTag in itemDir.Tags)
                {
                    if (itemTag.Name == "Duration" &&
                        !string.IsNullOrEmpty(itemTag.Description))
#pragma warning disable CA1305 // Specify IFormatProvider
                        return Convert.ToInt32(TimeSpan.Parse(itemTag.Description).TotalSeconds);
#pragma warning restore CA1305 // Specify IFormatProvider
                }
            }
            //var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
            //var dateTime = subIfdDirectory?.GetDescription(Exif.TagDateTime);
            return 0;/*
            
            var ffProbe = new NReco.VideoInfo.FFProbe();
            var videoInfo = ffProbe.GetMediaInfo(pathToVideoFile);
            return (int)Math.Ceiling(videoInfo.Duration.TotalSeconds);*/
        }

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
