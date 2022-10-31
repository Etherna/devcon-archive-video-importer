using System;
using System.Linq;
using System.Web;

namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class MDFileData
    {
        // Const.
        public const string PREFIX_ETHERNA_INDEX = "https://etherna.io/embed/";
        public const string PREFIX_ETHERNA_PERMALINK = "https://etherna.io/embed/";

        // Properties.
        public string Id { get; set; } = default!;
        public string? MdFilepath { get; set; }
        public string? Description { get; set; }
        public int Duration { get; set; }
        public int Edition { get; set; }
        public string? EthernaIndex { get; set; }
        public string? EthernaPermalink { get; set; }
        public string? Title { get; set; }
        public string? Type { get; set; }
        public string? YoutubeUrl { get; set; }
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
