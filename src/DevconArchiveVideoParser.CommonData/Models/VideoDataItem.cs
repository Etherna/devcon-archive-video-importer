using System;

namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class VideoDataItem
    {
        // Constructors.
        public VideoDataItem(
            int audioBitrate,
            string name,
            int resolution,
            Uri uri)
        {
            AudioBitrate = audioBitrate;
            Name = name;
            Resolution = resolution;
            Uri = uri;
        }

        // Properties.
        public int AudioBitrate { get; set; }
        public int Bitrate { get; set; }
        public int Duration { get; set; }
        public string? DownloadedFileName { get; set; }
        public string? DownloadedFilePath { get; set; }
        public string? DownloadedThumbnailPath { get; set; }
        public string? UploadedVideoReference { get; set; }
        public string Name { get; set; }
        public int Resolution { get; set; }
        public long Size { get; set; }
        public Uri Uri { get; set; }
    }
}
