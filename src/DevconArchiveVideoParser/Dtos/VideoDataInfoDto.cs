using System.Collections.Generic;

namespace DevconArchiveVideoParser.Dtos
{
    internal class VideoDataInfoDto
    {
        public string? Description { get; set; }
        public int Duration { get; set; }
        public int Edition { get; set; }
        public string? EthernaIndex { get; set; }
        public string? EthernaPermalink { get; set; }
        public string? IpfsHash { get; set; }
        public string? Title { get; set; }
        public string? Type { get; set; }
        public string? YoutubeUrl { get; set; }
    }
}
