using Etherna.DevconArchiveVideoImporter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using YoutubeExplode;
using YoutubeExplode.Converter;
using YoutubeExplode.Videos.Streams;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    internal class VideoDownloaderService : IVideoDownloaderService, IDisposable
    {
        // Const.
        private const int MAX_RETRY = 3;

        // Fields.
        private readonly string tmpFolder;
        private readonly HttpClient client = new();
        private readonly YoutubeClient youTubeClient = new();

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
                var videoResolutions = await DownloadAllResolutionAsync(videoData).ConfigureAwait(false);
                if (!videoResolutions.Any())
                    throw new InvalidOperationException($"Not found video");

                // Download thumbnail.
                var downloadedThumbnailPath = await DownloadThumbnailAsync(videoData).ConfigureAwait(false);
                var originalQuality = videoResolutions.First().Resolution;

                videoData.SetVideoResolutions(downloadedThumbnailPath, videoResolutions);

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
        private async Task<List<VideoDataResolution>> DownloadAllResolutionAsync(VideoData videoData)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));
            if (string.IsNullOrWhiteSpace(videoData.YoutubeUrl))
                throw new InvalidOperationException("Invalid youtube url");

            // Get manifest data
            var videoManifest = await youTubeClient.Videos.GetAsync(videoData.YoutubeUrl).ConfigureAwait(false);
            var streamManifest = await youTubeClient.Videos.Streams.GetManifestAsync(videoData.YoutubeUrl).ConfigureAwait(false);
            var streamInfos = streamManifest.GetMuxedStreams();

            // Get filename from video title
            var videoTitleBuilder = new StringBuilder(videoManifest.Title);
            foreach (char c in Path.GetInvalidFileNameChars())
                videoTitleBuilder = videoTitleBuilder.Replace(c, '_');
            var videoTitle = videoTitleBuilder.ToString();

            var resolutionVideoQuality = new List<string>();
            var sourceVideoInfos = new List<VideoDataResolution>();
            // Take muxed streams.
            var allResolutions = streamInfos
                .OrderBy(res => res.VideoResolution.Area)
                .Distinct();
            foreach (var currentRes in allResolutions)
            {
                resolutionVideoQuality.Add(currentRes.VideoQuality.Label);

                var videoDataResolution = await DownloadVideoAsync(
                    currentRes,
                    videoTitle,
                    videoManifest.Duration ?? TimeSpan.Zero).ConfigureAwait(false);
                sourceVideoInfos.Add(videoDataResolution);
            }

            if (ExistFFmpeg())
            {
                // Take highest quality MP4 video-only stream and highest bitrate audio-only stream
                var streamInfo = streamManifest
                    .GetVideoOnlyStreams()
                    .Where(stream => stream.Container == Container.Mp4)
                    .OrderBy(stream => stream.VideoResolution.Area);
                var bestStreamAudioInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
                foreach (var currentRes in streamInfo)
                {
                    if (resolutionVideoQuality.Contains(currentRes.VideoQuality.Label) ||
                        bestStreamAudioInfo == null)
                        continue;

                    resolutionVideoQuality.Add(currentRes.VideoQuality.Label);

                    var videoDataResolution = await DownloadVideoAndMuxAsync(
                        currentRes,
                        bestStreamAudioInfo,
                        videoTitle,
                        videoManifest.Duration ?? TimeSpan.Zero).ConfigureAwait(false);
                    sourceVideoInfos.Add(videoDataResolution);
                }
            }

            return sourceVideoInfos;
        }
        
        private async Task<VideoDataResolution> DownloadVideoAndMuxAsync(
            VideoOnlyStreamInfo videoOnlyStreamInfo,
            IStreamInfo audioOnlyStreamInfo,
            string videoTitle,
            TimeSpan duration)
        {
            var videoName = $"{videoTitle}_{videoOnlyStreamInfo.VideoResolution}";
            var videoDataResolution = new VideoDataResolution(
                videoOnlyStreamInfo.Bitrate.BitsPerSecond,
                Path.Combine(tmpFolder, $"{videoName}.muxed.mp4"),
                videoName,
                videoOnlyStreamInfo.VideoQuality.Label);

            var i = 0;
            var downloaded = false;
            while (i < MAX_RETRY &&
                    !downloaded)
                try
                {
                    i++;
                    var streamInfos = new IStreamInfo[] { audioOnlyStreamInfo, videoOnlyStreamInfo };

                    // Download and process them into one file
                    await youTubeClient.Videos.DownloadAsync(
                        streamInfos,
                        new ConversionRequestBuilder(videoDataResolution.DownloadedFilePath).SetFFmpegPath(GetFFmpegPath()).Build(),
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading resolution {videoDataResolution.Resolution} and mux ({(progressStatus * 100):N0}%) {videoOnlyStreamInfo.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);

                    downloaded = true;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            if (!downloaded)
                throw new InvalidOperationException($"Some error during download of video {videoOnlyStreamInfo.Url}");
            Console.WriteLine("");

            videoDataResolution.SetVideoInfo(
                videoName,
                videoOnlyStreamInfo.Size.Bytes,
                (int)duration.TotalSeconds);

            return videoDataResolution;
        }

        private async Task<VideoDataResolution> DownloadVideoAsync(
            MuxedStreamInfo muxedStreamInfo,
            string videoTitle,
            TimeSpan duration)
        {
            var videoName = $"{videoTitle}_{muxedStreamInfo.VideoResolution}";
            var videoFilepath = Path.Combine(tmpFolder, $"{videoName}.{muxedStreamInfo.Container}");
            var videoDataResolution = new VideoDataResolution(
                muxedStreamInfo.Bitrate.BitsPerSecond,
                videoFilepath,
                videoName,
                muxedStreamInfo.VideoQuality.Label);

            var i = 0;
            var downloaded = false;
            while (i < MAX_RETRY &&
                    !downloaded)
                try
                {
                    i++;
                    await youTubeClient.Videos.Streams.DownloadAsync(
                        muxedStreamInfo,
                        videoDataResolution.DownloadedFilePath,
                        new Progress<double>((progressStatus) =>
                        {
                            Console.Write($"Downloading resolution {videoDataResolution.Resolution} ({(progressStatus * 100):N0}%) {muxedStreamInfo.Size.MegaBytes:N2} MB\r");
                        })).ConfigureAwait(false);

                    downloaded = true;
                }
                catch { await Task.Delay(3500).ConfigureAwait(false); }
            if (!downloaded)
                throw new InvalidOperationException($"Some error during download of video {muxedStreamInfo.Url}");
            Console.WriteLine("");

            videoDataResolution.SetVideoInfo(
                videoName,
                muxedStreamInfo.Size.Bytes,
                (int)duration.TotalSeconds);

            return videoDataResolution;
        }

        private async Task<string?> DownloadThumbnailAsync(VideoData videoData)
        {
            if (videoData is null)
                throw new ArgumentNullException(nameof(videoData));
            if (string.IsNullOrWhiteSpace(videoData.YoutubeUrl))
                throw new InvalidOperationException("Invalid youtube url");

            // Get manifest data
            var videoManifest = await youTubeClient.Videos.GetAsync(videoData.YoutubeUrl).ConfigureAwait(false);

            var url = videoManifest.Thumbnails
                .OrderByDescending(thumbnail => thumbnail.Resolution.Area)
                .FirstOrDefault()
                ?.Url;
            if (string.IsNullOrWhiteSpace(url))
                return null;

            string filePath = $"{tmpFolder}/{videoManifest.Id}.jpg";
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
        
        private static bool ExistFFmpeg() =>
            File.Exists(GetFFmpegPath());

        private static string GetFFmpegPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "FFmpeg/ffmpeg.windows-64.exe";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "FFmpeg/ffmpeg.linux-64";
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "FFmpeg/ffmpeg.osx-64";

            throw new InvalidOperationException("OS not supported");
        }
    }
}
