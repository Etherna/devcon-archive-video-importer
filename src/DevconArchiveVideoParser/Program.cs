using System;
using System.Threading.Tasks;
using DevconArchiveVideoParser.Parsers;

namespace DevconArchiveVideoParser
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            if (args is null ||
                args.Length < 0)
            {
                Console.WriteLine("Missing read path");
                return;
            }
            if (args.Length < 1)
            {
                Console.WriteLine("Missing file CSV destination path");
                return;
            }

            // Read from files md.
            var videoDtos = MdParser.ToVideoDataDtos(args[0]);

            // Convert all dto to csv.
            await CsvParser.WriteFileAsync(args[1], videoDtos);
        }
    }
}