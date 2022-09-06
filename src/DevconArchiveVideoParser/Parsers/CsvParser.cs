using CsvHelper;
using DevconArchiveVideoParser.Dtos;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DevconArchiveVideoParser.Parsers
{
    internal class CsvParser
    {
        public static async Task WriteFileAsync(string csvDestination, IEnumerable<VideoDataInfoDto> records)
        {
            using (var writer = new StreamWriter(csvDestination))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
                await csv.WriteRecordsAsync(records);

            Console.WriteLine($"Csv with {records.Count()} items created in {csvDestination}");
        }
    }
}
