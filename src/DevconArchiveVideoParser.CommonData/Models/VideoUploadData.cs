namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class VideoUploadData
    {
        // Contractor.
        public VideoUploadData(
            int audioBitrate,
            string filename,
            int resolution,
            string? videoId,
            string uri)
        {
            AudioBitrate = audioBitrate;
            Filename = filename;
            Resolution = resolution;
            VideoId = videoId;
            Uri = uri;
        }

        // Properties.
        public int AudioBitrate { get; set; }
        public string? BatchId { get; set; }
        public string? BatchReferenceId { get; set; }
        public int Bitrate { get; set; }
        public int Duration { get; set; }
        public string? DownloadedFileName { get; set; }
        public string? DownloadedFilePath { get; set; }
        public string? DownloadedThumbnailPath { get; set; }
        public string? HashMetadataReference { get; set; }
        public string? IndexVideoId { get; set; }
        public string? EmbedDecentralizedLink { get; set; }
        public string? EmbedIndexLink { get; set; }
        public string Filename { get; set; }
        public int Resolution { get; set; }
        public string? Quality { get; set; }
        public long Size { get; set; }
        public string? ThumbnailReference { get; set; }
        public string? VideoId { get; set; }
        public string? VideoReference { get; set; }
        public string Uri { get; set; }
        public VideoMDData VideoMDData { get; set; } = default!;
    }
}
