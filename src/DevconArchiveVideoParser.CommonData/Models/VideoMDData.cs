using System.Collections.Generic;

namespace Etherna.DevconArchiveVideoParser.CommonData.Models
{
    public class VideoMDData
    {
        public string Id { get; set; } = default!;
        public string? Description { get; set; }
        public int Duration { get; set; }
        public string? EthernaUrl { get; set; }
        public string? MdFilepath { get; set; }
        public string? Title { get; set; }
        public string? YoutubeUrl { get; set; }
    }
}
