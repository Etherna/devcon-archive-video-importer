using System;
using System.Collections.Generic;
using System.Linq;

namespace DevconArchiveEthernaLinkReporter.Models
{
    internal class MdFile
    {
        private const string DurationPrefix = "duration";
        private const string EthernaIndexPrefix = "ethernaIndex";
        private const string EthernaPermalinkPrefix = "ethernaPermalink";
        private const string YouTubeUrlPrefix = "youtubeUrl";

        private readonly IEnumerable<string> rawLines;

        public MdFile(string path, IEnumerable<string> lines)
        {
            Path = path;

            rawLines = lines.Where(l => !l.StartsWith(EthernaIndexPrefix, StringComparison.OrdinalIgnoreCase) &&
                                        !l.StartsWith(EthernaPermalinkPrefix, StringComparison.OrdinalIgnoreCase));

            var ethernaIndexLine = lines.SingleOrDefault(line => line.StartsWith(EthernaIndexPrefix, StringComparison.OrdinalIgnoreCase));
            if (ethernaIndexLine is not null)
                EthernaIndex = ethernaIndexLine[(ethernaIndexLine.IndexOf(':', StringComparison.OrdinalIgnoreCase) + 1)..]
                    .Trim(' ', '"');

            var ethernaPermalinkLine = lines.SingleOrDefault(line => line.StartsWith(EthernaPermalinkPrefix, StringComparison.OrdinalIgnoreCase));
            if (ethernaPermalinkLine is not null)
                EthernaPermalink = ethernaPermalinkLine[(ethernaPermalinkLine.IndexOf(':', StringComparison.OrdinalIgnoreCase) + 1)..]
                    .Trim(' ', '"');

            var youtubeUrlLine = lines.SingleOrDefault(line => line.StartsWith(YouTubeUrlPrefix, StringComparison.OrdinalIgnoreCase));
            if (youtubeUrlLine is not null)
                YoutubeUrl = youtubeUrlLine[(youtubeUrlLine.IndexOf(':', StringComparison.OrdinalIgnoreCase) + 1)..]
                    .Trim(' ', '"');
        }

        public IEnumerable<string> Lines
        {
            get
            {
                var resultLines = new List<string>();
                foreach (var line in rawLines)
                {
                    if (line.StartsWith(DurationPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        if (EthernaIndex is not null)
                            resultLines.Add($"{EthernaIndexPrefix}: \"{EthernaIndex}\"");
                        if (EthernaPermalink is not null)
                            resultLines.Add($"{EthernaPermalinkPrefix}: \"{EthernaPermalink}\"");
                    }
                    resultLines.Add(line);
                }

                return resultLines;
            }
        }

        public string? EthernaIndex { get; set; }
        public string? EthernaPermalink { get; set; }
        public string? YoutubeUrl { get; }
        public string Path { get; }
    }
}
