using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;
using MyUsrn.Dnc.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Core.Tests
{
    public class FileTokenCacheArgument
    {
        string authority, tenantId, publicClientId, confidentialClientId, confidentialClientRedirectUri, confidentialClientSecret;
        //string appResource; // aka scope for custom web api of form appId/user_impersonation
        string aadGraphResource = "https://graph.windows.net/"; // aka scope for aad graph api
        string msftGraphResource = "https://graph.microsoft.com/"; // aka scope for msft graph api
        string[] scopes;
        string uniqueId, username, password, authorizationCode;

        public FileTokenCacheArgument()
        {
            // use this test skeleton to vet appssettings.json and appsettings.*.json configuration data retrieval used by Class/MemberData driven tests
            var config   = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
#if DEBUG
                .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)
#endif
                .AddJsonFile("appsettings.User.json", optional: true, reloadOnChange: true)
                .Build();

            //var configSection = config; // if settings not wrapped in outer object
            var configSection = config.GetSection("TokenCacheSpec"); // if you want to wrap settings in outer object

            var authorityBaseAddress = configSection["authorityBaseAddress"];
            tenantId = configSection["TenantId"];
            authority = $"{authorityBaseAddress}/{tenantId}"; // | /organizations | /consumers | /common
            publicClientId = configSection["PublicClientId"];
            confidentialClientId = configSection["ConfidentialClientId"];
            confidentialClientRedirectUri = configSection["ConfidentialClientRedirectUri"];
            confidentialClientSecret = configSection["ConfidentialClientId"];

            //appResource = configSection["AppResource"]; // | aadGraphResource | msftGraphResource
            scopes = new string[] { configSection["AppResource"] }; // | aadGraphResource | msftGraphResource

            uniqueId = configSection["UniqueId"];
            username = configSection["Username"];
            password = configSection["Password"];
            authorizationCode = configSection["AuthorizationCode"];
        }

        [Fact]
        public async Task Used_For_PublicClientApplication_Succeeds()
        {
            var app = new PublicClientApplication(publicClientId, authority, FileTokenCache.GetUserCache(/* cacheFilePath: "d://temp//my.msalcache.bin", */ cacheFileProtect: false));
            //var app = new PublicClientApplication(publicClientId, authority, RedisTokenCache.GetAppOrUserCache(uniqueId));
            var accounts = await app.GetAccountsAsync();
            AuthenticationResult authResult = null;

            try
            {
                // attempt to acquire valid token from cache or if expired use refresh token to silently acquire and cache new one
                authResult = await app.AcquireTokenSilentAsync(scopes, accounts.FirstOrDefault()); 
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenAsync to acquire a token.
                Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    //authResult = await app.AcquireTokenAsync(scopes); // not availabe in .net core environments
                    var securePassword = new NetworkCredential("", password).SecurePassword;
                    authResult = await app.AcquireTokenByUsernamePasswordAsync(scopes, username, securePassword);
                    //authResult = await app.AcquireTokenWithDeviceCodeAsync(scopes, deviceCodeResult =>
                    //{
                    //    // this will ouput the message telling automated test user where to go signin using browser and code to enter that will 
                    //    // complete this signin process w/o needing to have access to credentials here in unit, integration or web/load test code
                    //    Debug.WriteLine(deviceCodeResult.Message);
                    //    return Task.FromResult(0);
                    //}, CancellationToken.None);
                }
                catch (MsalException msalex)
                {
                    // ErrorCode: invalid_grant, StatusCode: 400 implies you need app registrations | {public client} | api permissions | grant admin consent for {tenant name}
                    Debug.WriteLine($"Error Acquiring Token:{System.Environment.NewLine}{msalex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}");
            }

            //var jwt = GetJsonWebToken(authResult.AccessToken); var actual = jwt["body"]["oid"].Value<string>();
            //var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authResult.AccessToken); var actual = jwt.Payload["oid"];
            var actual = authResult.UniqueId;

            var expected = uniqueId;
            Assert.True(actual == expected);
        }

        [Fact]
        public async Task Used_For_ConfidentialClientApplication_Succeeds()
        {
// msal acquiretokenbyauthorizationcodeasync ->
// https://azure.microsoft.com/en-us/resources/samples/active-directory-dotnet-webapp-openidconnect-v2/
// https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/wiki/Acquiring-tokens-with-authorization-codes-on-web-apps
// app registrations | {confidential client} | api permissions | add a permission | see best practices for requesting permissions ->
// https://docs.microsoft.com/en-us/azure/active-directory/develop/v1-permissions-and-consent

            var app = new ConfidentialClientApplication(confidentialClientId, confidentialClientRedirectUri,
                new ClientCredential(confidentialClientSecret), FileTokenCache.GetUserCache(cacheFileProtect: false), null);
                //new ClientCredential(confidentialClientSecret), RedisTokenCache.GetAppOrUserCache(uniqueId), null);
            var accounts = await app.GetAccountsAsync();
            AuthenticationResult authResult = null;

            //scopes = new string[] { msftGraphResource };
            //scopes = new string[] { "openid profile offline_access Mail.Read Mail.Send" };

            try
            {
                // attempt to acquire valid token from cache or if expired use refresh token to silently acquire and cache new one
                authResult = await app.AcquireTokenSilentAsync(scopes, accounts.FirstOrDefault());
            }
            catch (MsalUiRequiredException ex)
            {
                // A MsalUiRequiredException happened on AcquireTokenSilentAsync. This indicates you need to call AcquireTokenAsync to acquire a token.
                Debug.WriteLine($"MsalUiRequiredException: {ex.Message}");

                try
                {
                    authResult = await app.AcquireTokenByAuthorizationCodeAsync(authorizationCode, scopes);
                    //authResult = await app.AcquireTokenOnBehalfOfAsync(scopes, new UserAssertion(accessToken));
                }
                catch (MsalException msalex)
                {
                    // ErrorCode: invalid_grant, StatusCode: 400 implies you need ???
                    // ErrorCode: invalid_scope  StatusCode: 400 implies you need ???
                    Debug.WriteLine($"Error Acquiring Token:{System.Environment.NewLine}{msalex}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error Acquiring Token Silently:{System.Environment.NewLine}{ex}");
            }

            //var jwt = GetJsonWebToken(authResult.AccessToken); var actual = jwt["body"]["oid"].Value<string>();
            //var jwt = new JwtSecurityTokenHandler().ReadJwtToken(authResult.AccessToken); var actual = jwt.Payload["oid"];
            var actual = authResult.UniqueId;

            var expected = uniqueId;
            Assert.True(actual == expected);
        }

        /// <summary>
        /// Returns object containing the three json web token parts contained in encoded access token.
        /// </summary>
        /// <param name="encodedAccessToken"></param>
        /// <returns>A json object with header, body and signature properties.</returns>
        /// <remarks>In client layer since we have no way of safely maintaining token issuers signing cert then we have no way of verifiying signature
        /// and so you should not trust jwt property values the way that server side processing can given it can do signature verification.</remarks>
        JObject GetJsonWebToken(string encodedAccessToken)
        {
            if (string.IsNullOrEmpty(encodedAccessToken)) throw new ArgumentNullException("String to extract token from cannot be null or empty.");
                    
            var jwtValues = Base64StringDecodeEx(encodedAccessToken);
            if (jwtValues.Count != 3) throw new ArgumentOutOfRangeException("String to extract token from did not contain a header, body and signature.");

            var result = new JObject();

            var jwtHeader = JObject.Parse(jwtValues[0]);
            result.Add("header", jwtHeader);

            var jwtBody = JObject.Parse(jwtValues[1]);
            result.Add("body", jwtBody);

            result.Add("signature", jwtValues[2]);

            return result;
        }

        List<string> Base64StringDecodeEx(string arg)
        {
            if (string.IsNullOrEmpty(arg)) throw new ArgumentNullException("String to decode cannot be null or empty.");

            string[] values = new string[] { arg };
            if (arg.Contains(".")) values = arg.Split('.');

            List<string> result = new List<string>();
            foreach (var value in values)
            {
                result.Add(Base64StringDecode(value));
            }

            return result;
        }

        string Base64StringDecode(string arg)
        {
            if (string.IsNullOrEmpty(arg)) throw new ArgumentNullException("String to decode cannot be null or empty.");

            return Encoding.UTF8.GetString(ConvertFromBase64StringEx(arg));
        }

        byte[] ConvertFromBase64StringEx(string arg)
        {
            if (string.IsNullOrEmpty(arg)) throw new ArgumentNullException("String to convert cannot be null or empty.");

            StringBuilder s = new StringBuilder(arg);

// the following protects us from Convert.FromBase64String() throwing "System.FormatException: The input is not a valid Base-64 string as it contains a non-base 64 character, more than two padding characters, or an illegal character among the padding characters."
// which typically arises when processing token signature part and not the header or bodys parts
            const char Base64UrlCharacter62 = '-', Base64Character62 = '+'; s.Replace(Base64UrlCharacter62, Base64Character62);
            const char Base64UrlCharacter63 = '_', Base64Character63 = '/'; s.Replace(Base64UrlCharacter63, Base64Character63);

// the following protects us from Convert.FromBase64String() throwing "System.FormatException: Invalid length for a Base-64 char array or string."
// which typically arises when processing token body part and not the header or signature parts
            const char Base64PadCharacter = '=';
            int pad = s.Length % 4;
            s.Append(Base64PadCharacter, (pad == 0) ? 0 : 4 - pad);

            return Convert.FromBase64String(s.ToString());
        }
    }
}
