using System.Collections.Generic;

namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class VideoData
    {
        // Constructors.
        public VideoData(
            string downloadedThumbnailPath,
            MDFileData mdFileData,
            string originalQuality,
            ICollection<VideoDataItem> videoUploadItems)
        {
            DownloadedThumbnailPath = downloadedThumbnailPath;
            MDFileData = mdFileData;
            OriginalQuality = originalQuality;
            VideoDataItems = videoUploadItems;
        }

        // Properties.
        public string DownloadedThumbnailPath { get; set; }
        public MDFileData MDFileData { get; set; }
        public string OriginalQuality { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public ICollection<VideoDataItem> VideoDataItems { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
