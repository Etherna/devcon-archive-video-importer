using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using DevconArchiveVideoParser.Parsers;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.BeeNet;
using Etherna.DevconArchiveVideoParser.Services;
using Etherna.DevconArchiveVideoParser.SSO;
using IdentityModel.OidcClient;
using Etherna.DevconArchiveVideoParser.YoutubeDownloader.Clients;
using Etherna.BeeNet.Clients.GatewayApi;
using System.Text.Json;
using System.Formats.Asn1;
using System.Globalization;
using System.IO;
using Etherna.DevconArchiveVideoParser.Models;

namespace DevconArchiveVideoParser
{
    internal class Program
    {
        // Consts.
        private const string HelpText =
            "DevconArchiveVideoParser help:\n\n" +
            "-s\tSource folder path with *.md files to import\n" +
            "-o\tOutput csv file path\n" + "-m\tMax file video size (Mb)\n" +
            "-f\tFree video offer by creator\n" +
            "-p\tPin video\n" +
            "\n" +
            "-h\tPrint help\n";
        private const int BEENODE_GATEWAYPORT = 443;
        private const GatewayApiVersion BEENODE_GATEWAYVERSION = GatewayApiVersion.v3_0_2;
        private const string BEENODE_URL = "https://gateway.etherna.io/";
        private const string ETHERNA_INDEX = "https://index.etherna.io/";
        private const string ETHERNA_GATEWAY = "https://gateway.etherna.io/";
        private const string ETHERNA_INDEX_PARAMS_INFO = "api/v0.3/System/parameters";
        static readonly JsonSerializerOptions options = new()
        {
            PropertyNameCaseInsensitive = true
        };

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
            var videoMds = MdParser.ToVideoDataDtos(sourceFolderPath);

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
            var videoImporterService = new VideoImporterService(
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
                ETHERNA_GATEWAY,
                ETHERNA_INDEX,
                userEthAddr,
                offerVideo);

            // Call import service for each video.
            var indexParams = await GetParamsInfoAsync(httpClient).ConfigureAwait(false);
            var videoCount = 0;
            var totalVideo = videoMds.Count();
            foreach (var videoMd in videoMds)
            {
                try
                {
                    Console.WriteLine("===============================");
                    Console.WriteLine($"Start processing video #{++videoCount} of #{totalVideo}");
                    Console.WriteLine($"Title: {videoMd.Title}");

                    if (!string.IsNullOrWhiteSpace(videoMd.EthernaUrl))
                    {
                        //TODO check if somethings changed.
                        Console.WriteLine($"Video already on etherna");
                        continue;
                    }

                    if (videoMd.Title!.Length > indexParams.VideoTitleMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Title too long, max: {indexParams.VideoTitleMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    if (videoMd.Description!.Length > indexParams.VideoDescriptionMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Description too long, max: {indexParams.VideoDescriptionMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine($"Source Video: {videoMd.YoutubeUrl}");

                    // Download from youtube.
                    var videoUploadInfos = await videoImporterService.StartAsync(videoMd).ConfigureAwait(false);

                    if (videoMd.Duration <= 0)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Duration missing\n");
                        Console.ResetColor();
                        continue;
                    }

                    // Upload on bee node.
                    await videoUploaderService.StartAsync(videoUploadInfos, pinVideo).ConfigureAwait(false);

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
        private static async Task<IndexParamsDto> GetParamsInfoAsync(HttpClient httpClient)
        {
            var httpResponse = await httpClient.GetAsync(new Uri($"{ETHERNA_INDEX}{ETHERNA_INDEX_PARAMS_INFO}")).ConfigureAwait(false);

            httpResponse.EnsureSuccessStatusCode();

            var responseText = await httpResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<IndexParamsDto>(responseText, options)!;
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