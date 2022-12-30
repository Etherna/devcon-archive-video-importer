using Etherna.BeeNet;
using Etherna.BeeNet.Clients.DebugApi;
using Etherna.DevconArchiveVideoImporter.Dtos;
using Etherna.DevconArchiveVideoImporter.Services;
using Etherna.DevconArchiveVideoImporter.SSO;
using Etherna.DevconArchiveVideoImporter.Utilities;
using Etherna.ServicesClient;
using IdentityModel.OidcClient;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace Etherna.DevconArchiveVideoImporter
{
    internal class Program
    {
        // Consts.
        private const string HelpText =
            "DevconArchiveVideoImporter help:\n\n" +
            "-s\tSource folder path with *.md files to import\n" +
            "-f\tFree video offer by creator\n" +
            "-p\tPin video\n" +
            "-d\tDelete old videos that are no longer in the .MD files\n" +
            "-c\tDelete all index video with no valid manifest or old PersonalData\n" +
            "\n" +
            "-h\tPrint help\n";

        static async Task Main(string[] args)
        {
            // Parse arguments.
            string? sourceFolderPath = null;
            bool offerVideo = false;
            bool pinVideo = false;
            bool deleteVideo = false;
            bool cleanIndex = false;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-s": sourceFolderPath = args[++i]; break;
                    case "-f": offerVideo = true; break;
                    case "-p": pinVideo = true; break;
                    case "-d": deleteVideo = true; break;
                    case "-c": cleanIndex = true; break;
                    case "-h": Console.Write(HelpText); return;
                    default: throw new ArgumentException(args[i] + " is not a valid argument");
                }
            }

            // Request missing params.
            Console.WriteLine();
            Console.WriteLine("Source folder path with *.md files to import:");
            sourceFolderPath = ReadStringIfEmpty(sourceFolderPath);

            // Check tmp folder.
            const string tmpFolder = "tmpData";
            if (!Directory.Exists(tmpFolder))
                Directory.CreateDirectory(tmpFolder);
            var tmpFolderFullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, tmpFolder);

            // Read from files md.
            var mdVideos = MdVideoParserService.ToVideoDataDtos(sourceFolderPath);

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
            using var httpClient = new HttpClient(authResult.RefreshTokenHandler) { Timeout = TimeSpan.FromHours(2) };

            // Inizialize services.
            var ethernaUserClients = new EthernaUserClients(
                new Uri(CommonConst.ETHERNA_CREDIT),
                new Uri(CommonConst.ETHERNA_GATEWAY),
                new Uri(CommonConst.ETHERNA_INDEX),
                new Uri(CommonConst.SSO_AUTHORITY),
                () => httpClient);
            var ethernaClientService = new EthernaUserClientsAdapter(ethernaUserClients);
            using var videoDownloaderService = new VideoDownloaderService(tmpFolderFullPath);
            var beeNodeClient = new BeeNodeClient(
                CommonConst.ETHERNA_GATEWAY,
                CommonConst.BEENODE_GATEWAYPORT,
                null,
                CommonConst.BEENODE_GATEWAYVERSION,
                DebugApiVersion.v3_0_2,
                httpClient);
            var videoUploaderService = new VideoUploaderService(
                beeNodeClient,
                ethernaClientService,
                userEthAddr);
            // Import each video.
            var indexParams = await ethernaClientService.GetSystemParametersAsync().ConfigureAwait(false);
            var videoCount = 0;
            var totalVideo = mdVideos.Count();
            foreach (var video in mdVideos)
            {
                try
                {
                    Console.WriteLine("===============================");
                    Console.WriteLine($"Start processing video #{++videoCount} of #{totalVideo}");
                    Console.WriteLine($"Title: {video.Title}");

                    // Check last valid manifest, if exist.
                    var lastValidManifest = await ethernaClientService.GetLastValidManifestAsync(video.IndexVideoId).ConfigureAwait(false);
                    if (lastValidManifest is not null)
                    {
                        // Check if manifest contain the same url of current md file.
                        var personalData = JsonUtility.FromJson<MetadataPersonalDataDto>(lastValidManifest.PersonalData ?? "{}");
                        if (personalData is not null &&
                            personalData.VideoId == video.YoutubeId)
                        {
                            // When YoutubeId is already uploaded, check for any change in metadata.
                            if (video.Title != lastValidManifest.Title ||
                                video.Description != lastValidManifest.Description)
                            {
                                // No change in any fields.
                                Console.WriteLine($"Video already on etherna");
                                continue;
                            }
                            else
                            {
                                // Edit manifest data fields.
                                lastValidManifest.Description = video.Description ?? "";
                                lastValidManifest.Title = video.Title ?? "";
                            }
                        }
                        else
                        {
                            // Youtube video changed.
                            video.ResetEthernaData(); // Reset all data otherwise instead of create new index will be update.
                            lastValidManifest = null; // Set null for restart all process like a first time.
                        }
                    }

                    // Data validation.
                    if (video.Title!.Length > indexParams.VideoTitleMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Title too long, max: {indexParams.VideoTitleMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    if (video.Description!.Length > indexParams.VideoDescriptionMaxLength)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error: Description too long, max: {indexParams.VideoDescriptionMaxLength}\n");
                        Console.ResetColor();
                        continue;
                    }
                    Console.WriteLine($"Source Video: {video.YoutubeUrl}");

                    if (lastValidManifest is null)
                    {
                        // Download from source.
                        var videoData = await videoDownloaderService.StartDownloadAsync(video).ConfigureAwait(false);

                        if (videoData?.VideoDataResolutions is null ||
                            videoData.VideoDataResolutions.Count <= 0)
                        {
                            Console.ForegroundColor = ConsoleColor.DarkRed;
                            Console.WriteLine($"Error: video for download not found\n");
                            Console.ResetColor();
                            continue;
                        }

                        // Upload on bee node.
                        await videoUploaderService.UploadVideoAsync(videoData, pinVideo, offerVideo).ConfigureAwait(false);
                    }
                    else
                    {
                        // Change metadata info.
                        var hashMetadataReference = await videoUploaderService.UploadMetadataAsync(lastValidManifest, video, pinVideo).ConfigureAwait(false);
                        await ethernaClientService.UpsertManifestToIndex(hashMetadataReference, video).ConfigureAwait(false);
                    }

                    // Save MD file with etherna values.
                    Console.WriteLine($"Save etherna values in file {video.MdFilepath}\n");
                    var sourceMdFile = new LinkReporterService(video.MdFilepath!);
                    await sourceMdFile.SetEthernaFieldsAsync(
                        video.EthernaIndex!,
                        video.EthernaPermalink!).ConfigureAwait(false);

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

            // Delete old video.
            if (deleteVideo)
            {
                // Get video indexed
                var importedVideos = await ethernaClientService.GetAllUserVideoAsync(userEthAddr).ConfigureAwait(false);
                var videoIds = importedVideos.Select(
                        videoData => JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)?.VideoId)
                    .ToList();

                // Get video indexed but not in repository files *.md
                var removableIds = videoIds.Except(mdVideos.Select(repVideo => repVideo.YoutubeId).ToList());
                foreach (var videoId in removableIds)
                {
                    try
                    {
                        var itemToRemove = importedVideos.Where(
                        videoData => JsonUtility.FromJson<MetadataPersonalDataDto>(videoData?.LastValidManifest?.PersonalData)?.VideoId == videoId)
                            .First();

                        await ethernaClientService.DeleteIndexVideoAsync(itemToRemove.Id).ConfigureAwait(false);

                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine($"Video {itemToRemove.Id} removed");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error:{ex.Message} \n Video unable to remove video {videoId}\n");
                        Console.ResetColor();
                    }
                }
            }

            // User video.
            if (cleanIndex)
            {
                var videos = await ethernaClientService.GetAllUserVideoAsync(userEthAddr).ConfigureAwait(false);
                foreach (var video in videos)
                {
                    if (video.LastValidManifest is not null &&
                        !string.IsNullOrWhiteSpace(video.LastValidManifest.PersonalData))
                        continue;

                    try
                    {
                        await ethernaClientService.DeleteIndexVideoAsync(video.Id).ConfigureAwait(false);
                        Console.ForegroundColor = ConsoleColor.DarkGreen;
                        Console.WriteLine($"Video {video.Id} removed");
                        Console.ResetColor();
                    }
                    catch (Exception ex)
                    {
                        Console.ForegroundColor = ConsoleColor.DarkRed;
                        Console.WriteLine($"Error:{ex.Message} \n Video unable to remove video {video.Id}\n");
                        Console.ResetColor();
                    }
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
            var browser = new SystemBrowser(11420);
            var redirectUri = $"http://127.0.0.1:{browser.Port}";

            var options = new OidcClientOptions
            {
                Authority = CommonConst.SSO_AUTHORITY,
                ClientId = CommonConst.SSO_CLIENT_ID,
                RedirectUri = redirectUri,
                Scope = "openid profile offline_access ether_accounts userApi.gateway userApi.index",
                FilterClaims = false,

                Browser = browser,
                RefreshTokenInnerHttpHandler = new SocketsHttpHandler()
            };

            var oidcClient = new OidcClient(options);
            return await oidcClient.LoginAsync(new LoginRequest()).ConfigureAwait(false);
        }
    }
}