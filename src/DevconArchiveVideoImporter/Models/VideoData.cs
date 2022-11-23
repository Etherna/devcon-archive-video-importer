using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Etherna.DevconArchiveVideoImporter.Models
{
    public class VideoData
    {
        // Const.
        public const string PREFIX_ETHERNA_INDEX = "https://etherna.io/embed/";
        public const string PREFIX_ETHERNA_PERMALINK = "https://etherna.io/embed/";

        // Properties from file MD.
        public string Id { get; set; } = default!;
        public string? MdFilepath { get; set; }
        public string Description { get; set; } = default!;
        public int Duration { get; set; }
        public int Edition { get; set; }
        public string? EthernaIndex { get; set; }
        public string? EthernaPermalink { get; set; }
        public string Title { get; set; } = default!;
        public string? Type { get; set; }
        public string? YoutubeUrl { get; set; }


        // Properties from VideoSource.
#pragma warning disable CA2227 // Collection properties should be read only
        public ICollection<VideoDataResolution> VideoDataResolutions { get; set; } = default!;
#pragma warning restore CA2227 // Collection properties should be read only
        public string DownloadedThumbnailPath { get; set; } = default!;

        // Properties composed.
        public string? YoutubeId
        {
            get
            {
                if (string.IsNullOrWhiteSpace(YoutubeUrl))
                    return null;

                var uri = new Uri(YoutubeUrl);
                var query = HttpUtility.ParseQueryString(uri.Query);

                if (query != null &&
                    query.AllKeys.Contains("v"))
                    return query["v"];

                return uri.Segments.Last();
            }
        }
        public string? IndexVideoId
        {
            get
            {
                return EthernaIndex?.Replace(PREFIX_ETHERNA_INDEX, "", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        public string? PermalinkId
        {
            get
            {
                return EthernaIndex?.Replace(PREFIX_ETHERNA_PERMALINK, "", StringComparison.InvariantCultureIgnoreCase);
            }
        }

        // Methods.
        public void ResetEthernaData()
        {
            EthernaIndex = null;
            EthernaPermalink = null;
        }

        public string SetEthernaIndex(string indexVideoId)
        {
            EthernaIndex = $"{PREFIX_ETHERNA_INDEX}{indexVideoId}";
            return EthernaIndex;
        }

        public string SetEthernaPermalink(string hashMetadataReference)
        {
            EthernaPermalink = $"{PREFIX_ETHERNA_PERMALINK}{hashMetadataReference}";
            return EthernaPermalink;
        }

    }
}
