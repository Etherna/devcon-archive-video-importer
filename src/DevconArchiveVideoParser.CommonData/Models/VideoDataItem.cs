namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class VideoDataItem
    {
        // Contractor.
        public VideoDataItem(
            int audioBitrate,
            string filename,
            int resolution,
            string uri)
        {
            AudioBitrate = audioBitrate;
            Filename = filename;
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
        public string Filename { get; set; }
        public int Resolution { get; set; }
        public long Size { get; set; }
        public string Uri { get; set; }
    }
}
