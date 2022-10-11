using DevconArchiveEthernaLinkReporter.Models;
using System;
using System.IO;
using System.Linq;

namespace DevconArchiveEthernaLinkReporter
{
    internal class Program
    {
        // Consts.
        private const string HelpText =
            "DevconArchiveEthernaLinkReporter help:\n\n" +
            "-d\tDestination folder path with *.md files to modify\n" +
            "-o\tImporter output csv file path\n" +
            "\n" +
            "-h\tPrint help\n";

        // Methods.
        static void Main(string[] args)
        {
            // Parse arguments.
            string? destinationFolderPath = null;
            string? importerOutputCsvFilepath = null;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-d": destinationFolderPath = args[++i]; break;
                    case "-o": importerOutputCsvFilepath = args[++i]; break;
                    case "-h": Console.Write(HelpText); return;
                    default: throw new ArgumentException(args[i] + " is not a valid argument");
                }
            }

            // Request missing params.
            Console.WriteLine();
            Console.WriteLine("Destination folder path with *.md files to modify:");
            destinationFolderPath = ReadStringIfEmpty(destinationFolderPath);

            Console.WriteLine();
            Console.WriteLine("Importer output csv filepath:");
            importerOutputCsvFilepath = ReadStringIfEmpty(importerOutputCsvFilepath);
            Console.WriteLine();

            // Parse importer output.
            var csvVideos = Parsers.CsvParser.GetVideoRecords(importerOutputCsvFilepath);

            // Parse destination directory.
            var mdFilesPath = Directory.GetFiles(destinationFolderPath, "*.md", SearchOption.AllDirectories);
            var mdFiles = mdFilesPath.Select(p => new MdFile(p, File.ReadLines(p)));

            // Update md files.
            foreach (var video in csvVideos.Where(v => v.ImportStatus == "Processed"))
            {
                foreach (var file in mdFiles.Where(f => f.YoutubeUrl == video.YoutubeUrl))
                {
                    file.EthernaIndex = video.EmbedIndexLink;
                    file.EthernaPermalink = video.EmbedDecentralizedLink;

                    File.WriteAllLines(file.Path, file.Lines);
                }
            }
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