using System;
using System.Threading.Tasks;
using DevconArchiveVideoParser.Parsers;

namespace DevconArchiveVideoParser
{
    internal class Program
    {
        // Consts.
        private const string HelpText =
            "DevconArchiveVideoParser help:\n\n" +
            "-s\tSource folder path with *.md files to import\n" +
            "-o\tOutput csv file path\n" +
            "\n" +
            "-h\tPrint help\n";

        // Methods.
        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? sourceFolderPath = null;
            string? outputCsvFilepath = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-s": sourceFolderPath = args[++i]; break;
                    case "-o": outputCsvFilepath = args[++i]; break;
                    case "-h": Console.Write(HelpText); return;
                    default: throw new ArgumentException(args[i] + " is not a valid argument");
                }
            }

            // Request missing params.
            Console.WriteLine();
            Console.WriteLine("Source folder path with *.md files to import:");
            sourceFolderPath = ReadStringIfEmpty(sourceFolderPath);

            Console.WriteLine();
            Console.WriteLine("Output csv filepath:");
            outputCsvFilepath = ReadStringIfEmpty(outputCsvFilepath); 
            Console.WriteLine();

            // Read from files md.
            var videoDtos = MdParser.ToVideoDataDtos(sourceFolderPath);

            // Convert all dto to csv.
            await CsvParser.WriteFileAsync(outputCsvFilepath, videoDtos);
        }

        private static string ReadStringIfEmpty(string? strValue)
        {
            if (string.IsNullOrWhiteSpace(strValue))
            {
                while (string.IsNullOrWhiteSpace(strValue))
                {
                    strValue = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(strValue))
                        Console.WriteLine("*Empty string not allowed*");
                }
            }
            else Console.WriteLine(strValue);

            return strValue;
        }
    }
}