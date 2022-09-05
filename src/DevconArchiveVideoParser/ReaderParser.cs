using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DevconArchiveVideoParser
{
    internal class ReaderParser
    {
        public static readonly string[] _keywordForArrayString = { "KEYWORDS", "TAGS", "SPEAKERS" };
        public static readonly string[] _keywordSkips = { "IMAGE", "IMAGEURL" };
        public static readonly string[] _keywordNames = { "IMAGE", "IMAGEURL", "EDITION", "TITLE", "DESCRIPTION", "YOUTUBEURL", "IPFSHASH", "DURATION", "EXPERTISE", "TYPE", "TRACK", "KEYWORDS", "TAGS", "SPEAKERS" };

        public static IEnumerable<VideoDataInfoDto> StartParser(string folderRootPath)
        {
            var videoDataInfoDtos = new List<VideoDataInfoDto>();
            var files = Directory.GetFiles(folderRootPath, "*.md", SearchOption.AllDirectories);

            foreach (var sourceFile in files)
            {
                var itemConvertedToJson = new StringBuilder();
                var markerLine = 0;
                var keyFound = 0;
                var descriptionExtraRows = new List<string>();
                foreach (var line in File.ReadLines(sourceFile))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    if (_keywordSkips.Any(keyToSkip =>
                        line.StartsWith(keyToSkip, StringComparison.InvariantCultureIgnoreCase)))
                        continue;

                    if (line == "---")
                    {
                        markerLine++;

                        if (markerLine == 1)
                            itemConvertedToJson.AppendLine("{");
                        else if (markerLine == 2)
                        {
                            itemConvertedToJson.AppendLine("}");

                            VideoDataInfoDto? videoDataInfoDto = null;
                            try
                            {
                                videoDataInfoDto = JsonSerializer.Deserialize<VideoDataInfoDto>( 
                                    itemConvertedToJson.ToString(),
                                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                            }
#pragma warning disable CA1031 // Ignore exception
                            catch (Exception ex)
#pragma warning restore CA1031
                            {
                                Console.WriteLine($"Unable to parse file: {sourceFile}" + ex);
                            }

                            markerLine = 0;
                            keyFound = 0;
                            itemConvertedToJson = new StringBuilder();
                            if (videoDataInfoDto is not null)
                            {
                                videoDataInfoDto.Description += string.Join(". ", descriptionExtraRows);
                                videoDataInfoDtos.Add(videoDataInfoDto);
                            }
                        }
                    }
                    else
                    {
                        keyFound++;
                        itemConvertedToJson.AppendLine(FormatLineForJson(line, keyFound > 1, descriptionExtraRows));
                    }
                }
            }

            return videoDataInfoDtos;
        }

        private static string FormatLineForJson(string line, bool havePreviusRow, List<string> descriptionExtraRows)
        {
            if (string.IsNullOrWhiteSpace(line))
                return "";

            if (_keywordForArrayString.Any(keyArrayString =>
                    line.StartsWith(keyArrayString, StringComparison.InvariantCultureIgnoreCase)))
                line = line.Replace("'", "\"", StringComparison.InvariantCultureIgnoreCase); //Array of string change from ' to "

            // Prevent multiline description error 
            if (!_keywordNames.Any(keywordName =>
                    line.StartsWith(keywordName, StringComparison.InvariantCultureIgnoreCase)))
            {
                descriptionExtraRows.Add(line);
                return "";
            }

            var formatedString = (havePreviusRow ? "," : "") // Add , at end of every previus row (isFirstKeyFound used to avoid insert , in the last keyword)
                 + "\"" // Add " at start of every row
                + ReplaceFirstOccurrence(line, ":", "\":"); // Find the first : and add "

            // Prevent error for description multiline
            if (line.StartsWith("DESCRIPTION", StringComparison.InvariantCultureIgnoreCase) &&
                !formatedString.EndsWith("\"", StringComparison.InvariantCultureIgnoreCase))
                formatedString += "\"";

            return formatedString;
        }

        private static string ReplaceFirstOccurrence(string source, string find, string replace)
        {
            if (string.IsNullOrWhiteSpace(source))
                return "";

            var index = source.IndexOf(find, StringComparison.InvariantCultureIgnoreCase);
            string result = source.Remove(index, find.Length).Insert(index, replace);
            return result;
        }
    }
}
