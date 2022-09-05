using CsvHelper;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace DevconArchiveVideoParser
{
    internal class CsvAdapter
    {
        public static void WriteFile(string csvDestination, IEnumerable<VideoDataInfoDto> records)
        {
            using (var writer = new StreamWriter(csvDestination))
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                csv.WriteRecords(records);
            }
        }
    }
}
