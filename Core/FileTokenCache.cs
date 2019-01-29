using Microsoft.Identity.Client;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

/// <summary>
/// see https://aka.ms/msal-net-token-cache-serialization referenced in https://github.com/AzureAD/microsoft-authentication-library-for-dotnet/issues/763 discussion
/// </summary>
namespace MyUsrn.Dnc.Core
{
    public static class FileTokenCache
    {
        static TokenCache userTokenCache;

        /// <summary>
        /// Get the user token cache
        /// </summary>
        /// <returns></returns>
        public static TokenCache GetUserCache(/* string cacheFilePath = Assembly.GetExecutingAssembly().Location + ".msalcache.bin", */ bool cacheFileProtect = true)
        {
            //CacheFilePath = cacheFilePath;
            CacheFileProtect = cacheFileProtect;
            if (userTokenCache == null)
            {
                userTokenCache = new TokenCache();
                userTokenCache.SetBeforeAccess(BeforeAccessNotification);
                userTokenCache.SetAfterAccess(AfterAccessNotification);
            }
            return userTokenCache;
        }        

        static readonly object FileLock = new object();
        public static readonly string CacheFilePath = Assembly.GetExecutingAssembly().Location + ".msalcache.bin";
        public static /* readonly */ bool CacheFileProtect = false;

        // Triggered right before Msal needs to access the cache.
        // Reload the cache from the persistent store in case it changed since the last access.
        public static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            lock (FileLock)
            {
                args.TokenCache.Deserialize(File.Exists(CacheFilePath) ? 
                    (CacheFileProtect ? ProtectedData.Unprotect(File.ReadAllBytes(CacheFilePath), null, DataProtectionScope.CurrentUser) : File.ReadAllBytes(CacheFilePath))
                    : null);
            }
        }

        // Triggered right after Msal accessed the cache.
        public static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            // if the access operation resulted in a cache update
            if (args.TokenCache.HasStateChanged)
            {
                lock (FileLock)
                {
                    // reflect changesgs in the persistent store
                    File.WriteAllBytes(CacheFilePath, (CacheFileProtect ? ProtectedData.Protect(args.TokenCache.Serialize(), null, DataProtectionScope.CurrentUser) 
                        : args.TokenCache.Serialize()));
                    // once the write operationtakes place restore the HasStateChanged bit to filse
                    args.TokenCache.HasStateChanged = false;
                }
            }
        }

        // Empties the persistent store.
        public static void Clear()
        {
            File.Delete(CacheFilePath);
        }
    }
}
