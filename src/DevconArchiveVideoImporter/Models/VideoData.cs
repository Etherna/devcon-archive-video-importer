using Etherna.DevconArchiveVideoImporter.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Etherna.DevconArchiveVideoImporter.Models
{
    public class VideoData
    {
        // Properties from file MD.
        public string Id { get; protected set; } = default!;
        public string? MdFilepath { get; protected set; }
        public string Description { get; protected set; } = default!;
        public int Duration { get; protected set; }
        public int Edition { get; protected set; }
        public string? EthernaIndex { get; protected set; }
        public string? EthernaPermalink { get; protected set; }
        public string Title { get; protected set; } = default!;
        public string? Type { get; protected set; }
        public string? YoutubeUrl { get; protected set; }


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
        public string? IndexVideoId => EthernaIndex?.Replace(CommonConst.PREFIX_ETHERNA_INDEX, "", StringComparison.InvariantCultureIgnoreCase);

        public string? PermalinkId => EthernaIndex?.Replace(CommonConst.PREFIX_ETHERNA_PERMALINK, "", StringComparison.InvariantCultureIgnoreCase);

        // Methods.
        public void SetData(
            string id, 
            string mdFilepath)
        {
            Id = id;
            MdFilepath = mdFilepath;
        }

        public void AddDescription(IEnumerable<string> descriptions)
        {
            Description = Description ?? "";
            Description += string.Join(". ", descriptions);
        }

        public void ResetEthernaData()
        {
            EthernaIndex = null;
            EthernaPermalink = null;
        }

        public string SetEthernaIndex(string indexVideoId)
        {
            EthernaIndex = $"{CommonConst.PREFIX_ETHERNA_INDEX}{indexVideoId}";
            return EthernaIndex;
        }

        public string SetEthernaPermalink(string hashMetadataReference)
        {
            EthernaPermalink = $"{CommonConst.PREFIX_ETHERNA_PERMALINK}{hashMetadataReference}";
            return EthernaPermalink;
        }

    }
}
