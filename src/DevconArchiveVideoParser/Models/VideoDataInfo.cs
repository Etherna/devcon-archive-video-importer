namespace Etherna.DevconArchiveVideoParser.Models
{
    internal class VideoInfoWithData
    {
        // Video Infos.
        public string? Description { get; set; }
        public int Duration { get; set; }
        public int Edition { get; set; }
        public string? Title { get; set; }
        public string? IpfsHash { get; set; }
        public string? Type { get; set; }
        public string? YoutubeUrl { get; set; }

        // Video Data.
        public int Bitrate { get; set; }
        public string? BatchId { get; set; }
        public string? BatchReferenceId { get; set; }
        public string? DownloadedFileName { get; set; }
        public string? DownloadedFilePath { get; set; }
        public string? DownloadedThumbnailPath { get; set; }
        public string? IndexVideoId { get; set; }
        public string? HashMetadataReference { get; set; }
        public string? Quality { get; set; }
        public long Size { get; set; }
        public string? ThumbnailReference { get; set; }
        public string? VideoReference { get; set; }
        public ImportStatus? ImportStatus { get; set; }
        public CsvItemStatus? CsvItemStatus { get; set; }
        public string? VideoStatusNote { get; set; }
        public string? EmbedDecentralizedLink { get; set; }
        public string? EmbedIndexLink { get; set; }

    }
}
