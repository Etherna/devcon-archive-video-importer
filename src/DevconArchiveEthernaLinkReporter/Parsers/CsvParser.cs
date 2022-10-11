using CsvHelper;
using DevconArchiveEthernaLinkReporter.Dtos;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace DevconArchiveEthernaLinkReporter.Parsers
{
    internal class CsvParser
    {
        public static IEnumerable<ImporterVideoOutputDto> GetVideoRecords(string csvSource)
        {
            using var reader = new StreamReader(csvSource);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
            var records = csv.GetRecords<ImporterVideoOutputDto>();

            var videos = records.ToArray();
            Console.WriteLine($"Csv with {videos.Length} items readed from {csvSource}");

            return videos;
        }
    }
}
