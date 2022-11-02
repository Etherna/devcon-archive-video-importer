using Etherna.DevconArchiveVideoParser.CommonData.Interfaces;
using Etherna.DevconArchiveVideoParser.CommonData.Models;
using MetadataExtractor;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoParser.Services
{
    internal class VideoDownloaderService : IVideoDownloaderService
    {
        // Fields.
        private readonly IDownloadClient downloadClient;
        private readonly int? maxFilesize;
        private readonly string tmpFolder;
        private const int MAX_RETRY = 3;

        // Constractor.
        public VideoDownloaderService(
            IDownloadClient downloadClient,
            string tmpFolder,
            int? maxFilesize)
        {
            this.downloadClient = downloadClient;
            this.maxFilesize = maxFilesize;
            this.tmpFolder = tmpFolder;
        }

        // Public methods.
        public async Task<VideoData> StartDownloadAsync(MDFileData videoMd)
        {
            if (string.IsNullOrWhiteSpace(videoMd?.YoutubeUrl))
                throw new InvalidOperationException("Invalid YoutubeUrl");

            try
            {
                // Take best video resolution.
                var videoResolutions = await downloadClient.DownloadAllResolutionVideoAsync(videoMd, maxFilesize).ConfigureAwait(false);
                if (videoResolutions is null ||
                    videoResolutions.Count == 0)
                    throw new InvalidOperationException($"Not found video");

                // Download each video reoslution.
                foreach (var videoInfo in videoResolutions)
                {
                    // Start download and show progress.
                    videoInfo.DownloadedFilePath = Path.Combine(tmpFolder, videoInfo.Filename);

                    var i = 0;
                    var downloaded = false;
                    while (i < MAX_RETRY &&
                            !downloaded)
                        try
                        {
                            i++;
                            await downloadClient.DownloadAsync(
                                videoInfo.Uri,
                                videoInfo.DownloadedFilePath,
                                new Progress<(long totalBytesCopied, long fileSize)>((progressStatus) =>
                                {
                                    var percent = (int)(progressStatus.totalBytesCopied * 100 / progressStatus.fileSize);
                                    Console.Write($"Downloading resolution {videoInfo.Resolution}.. ( % {percent} ) {progressStatus.totalBytesCopied / (1024 * 1024)} / {progressStatus.fileSize / (1024 * 1024)} MB\r");
                                })).ConfigureAwait(false);
                            downloaded = true;
                        }
                        catch { }
                    if (!downloaded)
                        throw new InvalidOperationException($"Some error during download of video {videoInfo.Uri}");
                    Console.WriteLine("");

                    // Set video info from downloaded video.
                    var fileSize = new FileInfo(videoInfo.DownloadedFilePath!).Length;
                    videoInfo.DownloadedFileName = videoInfo.Filename;
                    videoInfo.Size = fileSize;
                    videoInfo.Duration = GetDuration(videoInfo.DownloadedFilePath);
                    if (videoInfo.Duration <= 0)
                    {
                        throw new InvalidOperationException($"Invalid Duration: {videoInfo.Duration}");
                    }
                    videoInfo.Bitrate = (int)Math.Ceiling((double)fileSize * 8 / videoInfo.Duration);
                }

                // Download Thumbnail.
                var downloadedThumbnailPath = await downloadClient.DownloadThumbnailAsync(videoMd.YoutubeId!, tmpFolder).ConfigureAwait(false);
                var originalQuality = videoResolutions.First().Resolution + "p";

                return new VideoData(downloadedThumbnailPath, videoMd, originalQuality, videoResolutions);
            }
            catch (Exception)
            {
                throw;
            }
        }

        // Private Methods.
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
    }
}
