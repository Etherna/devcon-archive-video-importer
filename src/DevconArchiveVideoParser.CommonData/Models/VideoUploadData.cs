using System.Collections.Generic;

namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class VideoUploadData
    {
        // Contractor.
        public VideoUploadData(
            string downloadedThumbnailPath,
            MDFileData mdFileData,
            string originalQuality,
            ICollection<VideoUploadDataItem> videoUploadItems)
        {
            DownloadedThumbnailPath = downloadedThumbnailPath;
            MDFileData = mdFileData;
            OriginalQuality = originalQuality;
            VideoUploadDataItems = videoUploadItems;
        }

        // Properties.
        public string DownloadedThumbnailPath { get; set; }
        public MDFileData MDFileData { get; set; }
        public string OriginalQuality { get; set; }
#pragma warning disable CA2227 // Collection properties should be read only
        public ICollection<VideoUploadDataItem> VideoUploadDataItems { get; set; }
#pragma warning restore CA2227 // Collection properties should be read only
    }
}
