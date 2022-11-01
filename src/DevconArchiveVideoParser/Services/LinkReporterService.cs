using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoParser.Services
{
    internal class LinkReporterService
    {
        private const string DurationPrefix = "duration:";
        private const string EthernaIndexPrefix = "ethernaIndex:";
        private const string EthernaPermalinkPrefix = "ethernaPermalink:";

        private readonly string mdFilePath;

        public LinkReporterService(string mdFilePath)
        {
            if (string.IsNullOrWhiteSpace(mdFilePath))
                throw new ArgumentNullException(nameof(mdFilePath));

            this.mdFilePath = mdFilePath;
        }

        public async Task SetEthernaValueAsync(
            string ethernaIndex,
            string ethernaPermalink,
            int duration)
        {
            // Reaad all line.
            var lines = File.ReadLines(mdFilePath).ToList();

            // Set ethernaIndex.
            var index = GetLineNumber(lines, EthernaIndexPrefix);
            var ethernaIndexValue = $"{EthernaIndexPrefix} \"{ethernaIndex}\"";
            if (index >= 0)
                lines[index] = ethernaIndexValue;
            else
                lines.Insert(GetIndexOfInsertLine(lines.Count), ethernaIndex);

            // Set ethernaPermalink.
            index = GetLineNumber(lines, EthernaPermalinkPrefix);
            var ethernaIndexLineValue = $"{EthernaPermalinkPrefix} \"{ethernaPermalink}\"";
            if (index >= 0)
                lines[index] = ethernaIndexLineValue;
            else
                lines.Insert(GetIndexOfInsertLine(lines.Count), ethernaIndexLineValue);

            // Set duration.
            index = GetLineNumber(lines, DurationPrefix);
            var durationLineValue = $"{DurationPrefix} \"{duration}\"";
            if (index >= 0)
                lines[index] = durationLineValue;
            else
                lines.Insert(GetIndexOfInsertLine(lines.Count), durationLineValue);

            // Save file.
            await File.WriteAllLinesAsync(mdFilePath, lines).ConfigureAwait(false);
        }

        // Helpers.
        private int GetLineNumber(List<string> lines, string prefix)
        {
            var lineIndex = 0;
            foreach (var line in lines)
            {
                if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return lineIndex;

                lineIndex++;
            }
            return -1;
        }

        private int GetIndexOfInsertLine(int lines)
        {
            // Last position. (Exclueded final ---)
            if (lines > 1)
                return lines - 2;
            else if (lines == 1)
                return 1;
            return 0;
        }
    }
}
