using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet.Clients.GatewayApi;
using Etherna.DevconArchiveVideoParser.CommonData.Models;
using Etherna.DevconArchiveVideoParser.Services;
using Etherna.DevconArchiveVideoParser.SSO;
using Etherna.DevconArchiveVideoParser.YoutubeDownloader.Clients;
using IdentityModel.OidcClient;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace DevconArchiveVideoParser
{
    internal class Program
    {
        // Consts.
        private const string HelpText =
            "DevconArchiveVideoParser help:\n\n" +
            "-s\tSource folder path with *.md files to import\n" +
            "-o\tOutput csv file path\n" +
            "-m\tMax file video size (Mb)\n" +
            "-f\tFree video offer by creator\n" +
            "-p\tPin video\n" +
            "\n" +
            "-h\tPrint help\n";
        private const int BEENODE_GATEWAYPORT = 443;
        private const GatewayApiVersion BEENODE_GATEWAYVERSION = GatewayApiVersion.v3_0_2;
        private const string BEENODE_URL = "https://gateway.etherna.io/";
        private const string ETHERNA_INDEX = "https://index.etherna.io/";
        private const string ETHERNA_GATEWAY = "https://gateway.etherna.io/";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? sourceFolderPath = null;
            string? maxFilesizeStr = null;
            bool offerVideo = false;
            bool pinVideo = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-s": sourceFolderPath = args[++i]; break;
                    case "-m": maxFilesizeStr = args[++i]; break;
                    case "-f": offerVideo = true; break;
                    case "-p": pinVideo = true; break;
                    case "-h": Console.Write(HelpText); return;
                    default: throw new ArgumentException(args[i] + " is not a valid argument");
                }
            }

            // Request missing params.
            Console.WriteLine();
            Console.WriteLine("Source folder path with *.md files to import:");
            sourceFolderPath = ReadStringIfEmpty(sourceFolderPath);

            int? maxFilesize = null;
            if (!string.IsNullOrWhiteSpace(maxFilesizeStr))
                if (!int.TryParse(maxFilesizeStr, out int convertedFileSize))
                    Console.WriteLine("Invalid input for max filesize, will be used unlimited size");
                else
                    maxFilesize = convertedFileSize;

            // Check tmp folder.
            const string tmpFolder = "tmpData";
            if (!Directory.Exists(tmpFolder))
                Directory.CreateDirectory(tmpFolder);
            var tmpFolderFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tmpFolder);

            // Read from files md.
            var mdFiles = MdVideoParserService.ToVideoDataDtos(sourceFolderPath);

            // Sign with SSO and create auth client.
            var authResult = await SigInSSO().ConfigureAwait(false);
            if (authResult.IsError)
            {
                Console.WriteLine($"Error during authentication");
                Console.WriteLine(authResult.Error);
                return;
            }
            var userEthAddr = authResult.User.Claims.Where(i => i.Type == "ether_address").FirstOrDefault()?.Value;
            if (string.IsNullOrWhiteSpace(userEthAddr))
            {
                Console.WriteLine($"Missing ether address");
                return;
            }
            var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromHours(2) };

            // Inizialize services.
            var indexerService = new IndexerService(httpClient, ETHERNA_INDEX);
            var videoDownloaderService = new VideoDownloaderService(
                new YoutubeDownloadClient(),
                tmpFolderFullPath,
                maxFilesize);
            var beeNodeClient = new BeeNodeClient(
                    BEENODE_URL,
                    BEENODE_GATEWAYPORT,
                    null,
                    BEENODE_GATEWAYVERSION,
                    DebugApiVersion.v3_0_2,
                    httpClient);
            var videoUploaderService = new VideoUploaderService(
                httpClient,
                beeNodeClient,
                indexerService,
                ETHERNA_GATEWAY,
                userEthAddr,
                offerVideo);

            // Import each video.
            var indexParams = await indexerService.GetParamsInfoAsync().ConfigureAwait(false);
            var videoCount = 0;
            var totalVideo = mdFiles.Count();
            foreach (var mdFile in mdFiles)
            {
                try
                {
                    Console.WriteLine("===============================");
                    Console.WriteLine($"Start processing video #{++videoCount} of #{totalVideo}");
                    Console.WriteLine($"Title: {mdFile.Title}");

                    // Check last valid manifest, if exist.
                    var manifest = await indexerService.GetLastValidManifestAsync(mdFile.IndexVideoId).ConfigureAwait(false);
                    if (manifest is not null)
                    {
                        // Check if manifest contain the same url of current md file.
                        var extraInfo = manifest.ExtraInfoTyped<MetadataExtraInfo>();
                        if (extraInfo is not null &&
                            extraInfo.VideoId == mdFile.YoutubeId)
                        {
                            // When YoutubeId is already uploaded, check for any change in metadata.
                            if (!manifest.CheckForMetadataInfoChanged(mdFile))
                            {
                                // No change in any fields.
                                Console.WriteLine($"Video already on etherna");
                                continue;
                            }
                            else
                                manifest.UpdateMetadataInfo(mdFile);
                        }
                        else
                        {
                            // Youtube video changed.
                            // TODO remove all old Indexed data.
                            mdFile.ResetEthernaData(); // Reset all data otherwise instead of creane new index will be update.
                            manifest = null; // Set null for restart all process like a first time.
                        }
                    }

                    // Data validation.
                    if (mdFile.Title!.Length > indexParams.VideoTitleMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Title too long, max: {indexParams.VideoTitleMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    if (mdFile.Description!.Length > indexParams.VideoDescriptionMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Description too long, max: {indexParams.VideoDescriptionMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine($"Source Video: {mdFile.YoutubeUrl}");

                    int duration;
                    if (manifest is null)
                    {
                        // Download from youtube.
                        var videoToUploadInfos = await videoDownloaderService.StartAsync(mdFile).ConfigureAwait(false);

                        if (videoToUploadInfos?.VideoUploadDataItems is null ||
                            videoToUploadInfos.VideoUploadDataItems.Count <= 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: video for download not found\n");
                            Console.ResetColor();
                            continue;
                        }

                        // Upload on bee node.
                        await videoUploaderService.StartUploadAsync(videoToUploadInfos, pinVideo).ConfigureAwait(false);

                        // Take duration from one of downloaded videos.
                        duration = videoToUploadInfos.VideoUploadDataItems.First().Duration;
                    }
                    else
                    {
                        // Change metadata info.
                        var hashMetadataReference = await videoUploaderService.UploadMetadataAsync(manifest, mdFile, pinVideo).ConfigureAwait(false);
                        await indexerService.IndexManifestAsync(hashMetadataReference, mdFile).ConfigureAwait(false);

                        // Take duration from previues md file saved.
                        duration = mdFile.Duration;
                    }

                    // Save MD file with etherna values.
                    Console.WriteLine($"Save etherna values in file {mdFile.MdFilepath}\n");
                    var sourceMdFile = new LinkReporterService(mdFile.MdFilepath!);
                    await sourceMdFile.SetEthernaValueAsync(
                        mdFile.EthernaIndex!,
                        mdFile.EthernaPermalink!,
                        duration).ConfigureAwait(false);

                    // Import completed.
                    Console.ForegroundColor = ConsoleColor.DarkGreen;
                    Console.WriteLine($"#{videoCount} Video imported successfully");
                    Console.ResetColor();
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.WriteLine($"Error:{ex.Message} \n#{videoCount} Video unable to import\n");
                    Console.ResetColor();
                }
            }
        }

        // Private helpers.
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

        private static async Task<LoginResult> SigInSSO()
        {
            // create a redirect URI using an available port on the loopback address.
            // requires the OP to allow random ports on 127.0.0.1 - otherwise set a static port
            var browser = new SystemBrowser(59100);
            var redirectUri = $"http://127.0.0.1:{browser.Port}";

            var options = new OidcClientOptions
            {
                Authority = "https://sso.etherna.io/",
                ClientId = "ethernaVideoImporterId",
                RedirectUri = redirectUri,
                Scope = "openid profile offline_access ether_accounts userApi.gateway userApi.index",
                FilterClaims = false,

                Browser = browser,
                IdentityTokenValidator = new JwtHandlerIdentityTokenValidator(),
                RefreshTokenInnerHttpHandler = new SocketsHttpHandler()
            };

            var oidcClient = new OidcClient(options);
            return await oidcClient.LoginAsync(new LoginRequest()).ConfigureAwait(false);
        }
    }
}