using Etherna.EthernaVideoImporter.Services;
using Etherna.EthernaVideoImporter.SSO;
using Etherna.EthernaVideoImporter.Utilities;
using Etherna.ServicesClient;
using IdentityModel.OidcClient;

namespace Etherna.DevconArchiveVideoImporter
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
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
            var ethernaClientService = new EthernaService(ethernaUserClients);

            // User video.
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

        // Private helpers.

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