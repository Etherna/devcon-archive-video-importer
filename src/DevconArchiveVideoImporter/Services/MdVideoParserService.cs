using Etherna.DevconArchiveVideoImporter.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace Etherna.DevconArchiveVideoImporter.Services
{
    internal sealed class MdVideoParserService
    {
        // Fields.
        public static readonly string[] _keywordForArrayString = Array.Empty<string>();
        public static readonly string[] _keywordSkips = { "IMAGE", "IMAGEURL", "IPFSHASH", "EXPERTISE", "TRACK", "KEYWORDS", "TAGS", "SPEAKERS", "SOURCEID" };
        public static readonly string[] _keywordNames = { "IMAGE", "IMAGEURL", "EDITION", "TITLE", "DESCRIPTION", "YOUTUBEURL", "IPFSHASH", "DURATION", "EXPERTISE", "TYPE", "TRACK", "KEYWORDS", "TAGS", "SPEAKERS", "ETHERNAINDEX", "ETHERNAPERMALINK", "SOURCEID" };

        // Methods.
        public static IEnumerable<VideoData> ToVideoDataDtos(string folderRootPath)
        {
            var videoDataInfoDtos = new List<VideoData>();
            var files = Directory.GetFiles(folderRootPath, "*.md", SearchOption.AllDirectories);

            Console.WriteLine($"Total files: {files.Length}");

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

                            VideoData? videoDataInfoDto = null;
                            try
                            {
                                videoDataInfoDto = JsonSerializer.Deserialize<VideoData>(
                                    itemConvertedToJson.ToString(),
                                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                                if (videoDataInfoDto is not null)
                                {
                                    videoDataInfoDto.SetData(
                                        sourceFile!.Replace(folderRootPath, "", StringComparison.InvariantCultureIgnoreCase),
                                        sourceFile);
                                    videoDataInfoDtos.Add(videoDataInfoDto);
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"{ex.Message} \n Unable to parse file: {sourceFile}");
                            }

                            markerLine = 0;
                            keyFound = 0;
                            itemConvertedToJson = new StringBuilder();
                            videoDataInfoDto?.AddDescription(descriptionExtraRows);
                        }
                    }
                    else
                    {
                        keyFound++;
                        itemConvertedToJson.AppendLine(FormatLineForJson(line, keyFound > 1, descriptionExtraRows));
                    }
                }
            }

            return videoDataInfoDtos.OrderBy(item => item.Edition);
        }

        // Helper.
        private static string FormatLineForJson(string line, bool havePreviusRow, List<string> descriptionExtraRows)
        {
            if (string.IsNullOrWhiteSpace(line))
                return "";

            // Fix error declaration of speakers.
            line = line.Replace("\"G. Nicholas D'Andrea\"", "'G. Nicholas D\"Andrea'", StringComparison.InvariantCultureIgnoreCase);
            if (line.Contains("", StringComparison.InvariantCultureIgnoreCase))
                if (_keywordForArrayString.Any(keyArrayString =>
                        line.StartsWith(keyArrayString, StringComparison.InvariantCultureIgnoreCase) &&
                        line.Contains("['", StringComparison.InvariantCultureIgnoreCase)))
                {
                    //array of string change from ' to "
                    //use TEMPSINGLEQUOTE for mange the case like        'Piotrek "Viggith" Janiuk'
                    line = line.Replace("'", "TEMPSINGLEQUOTE", StringComparison.InvariantCultureIgnoreCase);
                    line = line.Replace("\"", "'", StringComparison.InvariantCultureIgnoreCase);
                    line = line.Replace("TEMPSINGLEQUOTE", "\"", StringComparison.InvariantCultureIgnoreCase);
                }

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

            return formatedString.Replace("\t", " ", StringComparison.InvariantCultureIgnoreCase); // Replace \t \ with space
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
