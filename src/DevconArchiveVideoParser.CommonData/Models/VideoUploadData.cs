namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class VideoUploadData
    {
        // Contractor.
        public VideoUploadData(
            int audioBitrate,
            string filename,
            int resolution,
            string uri,
            MDFileData mdFileData)
        {
            AudioBitrate = audioBitrate;
            Filename = filename;
            Resolution = resolution;
            Uri = uri;
            MDFileData = mdFileData;
        }

        // Properties.
        public int AudioBitrate { get; set; }
        public int Bitrate { get; set; }
        public int Duration { get; set; }
        public string? DownloadedFileName { get; set; }
        public string? DownloadedFilePath { get; set; }
        public string? DownloadedThumbnailPath { get; set; }
        public string Filename { get; set; }
        public int Resolution { get; set; }
        public string? Quality { get; set; }
        public long Size { get; set; }
        public string Uri { get; set; }
        public MDFileData MDFileData { get; set; }
    }
}
