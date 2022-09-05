using System;

namespace DevconArchiveVideoParser
{
    internal class Program
    {
        static void Main(string[] args)
        {
            args = new[] { "D:\\tmp\\devcon-website-archive\\src\\content\\archive\\videos", "testfile.csv" };

            if (//args is null ||
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

            var videoDtos = ReaderParser.StartParser(args[0]);
            CsvAdapter.WriteFile(args[1], videoDtos);
        }
    }
}