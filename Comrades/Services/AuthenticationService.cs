using Azure.Identity;
using Microsoft.Graph.Beta;
using Microsoft.Identity.Client;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Comrades.Services
{
    public class AuthenticationService
    {
        //Set the scope for API call to user.read
        private static string[] scopes = new string[] { "Team.ReadBasic.All", "Group.ReadWrite.All", "TeamSettings.ReadWrite.All", "Files.ReadWrite.All", "ChannelSettings.ReadWrite.All", "Channel.Delete.All", "TeamsTab.ReadWrite.All", "Calendars.ReadWrite", "AppCatalog.ReadWrite.All", "TeamsAppInstallation.ReadWriteForTeam", "ChannelMessage.Send", "ChannelMessage.Read.All", "Chat.ReadWrite" };

        // Below are the clientId (Application Id) of your app registration and the tenant information.
        // You have to replace:
        // - the content of ClientID with the Application Id for your app registration
        private const string ClientId = "redacted";

        private const string Tenant = "common"; // Alternatively "[Enter your tenant, as obtained from the Azure portal, e.g. kko365.onmicrosoft.com]"
        private const string Authority = "https://login.microsoftonline.com/" + Tenant;

        // The MSAL Public client app
        private static IPublicClientApplication PublicClientApp;

        private static string AUTH_RECORD = "authrecord.bin";
        private static AuthenticationResult authResult;

        /// <summary>
        /// Signs in the user and obtains an access token for Microsoft Graph
        /// </summary>
        /// <param name="scopes"></param>
        /// <returns> Access Token</returns>
        private static async Task<string> SignInUserAndGetTokenUsingMSAL(string[] scopes)
        {
            // Initialize the MSAL library by building a public client application
            PublicClientApp = PublicClientApplicationBuilder.Create(ClientId)
                .WithAuthority(Authority)
                .WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient")
                 .WithLogging((level, message, containsPii) =>
                 {
                     Debug.WriteLine($"MSAL: {level} {message} ");
                 }, LogLevel.Warning, enablePiiLogging: false, enableDefaultPlatformLogging: true)
                .Build();

            // It's good practice to not do work on the UI thread, so use ConfigureAwait(false) whenever possible.
            IEnumerable<IAccount> accounts = await PublicClientApp.GetAccountsAsync().ConfigureAwait(false);
            IAccount firstAccount = accounts.FirstOrDefault();

            try
            {
                authResult = await PublicClientApp.AcquireTokenSilent(scopes, firstAccount)
                                                  .ExecuteAsync();
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenAsync to acquire a token
                Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                authResult = await PublicClientApp.AcquireTokenInteractive(scopes)
                                                  .ExecuteAsync()
                                                  .ConfigureAwait(false);

            }
            return authResult.AccessToken;
        }

        public static async Task<GraphServiceClient> GetGraphService()
        {
            InteractiveBrowserCredential credential;
            AuthenticationRecord authRecord;

            if (!File.Exists(Path.Join(Windows.Storage.ApplicationData.Current.LocalFolder.Path, AUTH_RECORD)))
            {
                credential = new InteractiveBrowserCredential(new InteractiveBrowserCredentialOptions
                {
                    TenantId = Tenant,
                    ClientId = ClientId,
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                    // MUST be http://localhost or http://localhost:PORT
                    // See https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/System-Browser-on-.Net-Core
                    RedirectUri = new Uri("http://localhost"),
                    TokenCachePersistenceOptions = new TokenCachePersistenceOptions { Name = "Comrades_Cache" }
                });

                // Call AuthenticateAsync to fetch a new AuthenticationRecord.
                authRecord = await credential.AuthenticateAsync();

                // Serialize the AuthenticationRecord to disk so that it can be re-used across executions of this initialization code.
                using var authRecordStream = new FileStream(Path.Join(Windows.Storage.ApplicationData.Current.LocalFolder.Path, AUTH_RECORD), FileMode.Create, FileAccess.Write);
                await authRecord.SerializeAsync(authRecordStream);
            }
            else
            {
                // Load the previously serialized AuthenticationRecord from disk and deserialize it.
                using var authRecordStream = new FileStream(Path.Join(Windows.Storage.ApplicationData.Current.LocalFolder.Path, AUTH_RECORD), FileMode.Open, FileAccess.Read);
                authRecord = await AuthenticationRecord.DeserializeAsync(authRecordStream);

                var options = new InteractiveBrowserCredentialOptions
                {
                    TenantId = Tenant,
                    ClientId = ClientId,
                    AuthorityHost = AzureAuthorityHosts.AzurePublicCloud,
                    // MUST be http://localhost or http://localhost:PORT
                    // See https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/System-Browser-on-.Net-Core
                    RedirectUri = new Uri("http://localhost"),
                    TokenCachePersistenceOptions = new TokenCachePersistenceOptions() { Name = "Comrades_Cache", UnsafeAllowUnencryptedStorage = true },
                    AuthenticationRecord = authRecord
                };

                credential = new InteractiveBrowserCredential(options);
            }

            GraphServiceClient graphClient = new GraphServiceClient(credential, scopes);

            return await Task.FromResult(graphClient);
        }
    }
}
