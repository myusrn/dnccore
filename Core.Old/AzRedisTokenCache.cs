using Microsoft.Identity.Client;
using Newtonsoft.Json;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Web;

// derived from the following aka.ms/aadSamples provided TokenCache implementations
// https://github.com/Azure-Samples/active-directory-dotnet-native-headless.git | TodoListClient | FileCache.cs
// https://github.com/Azure-Samples/active-directory-dotnet-webapi-onbehalfof.git | TodoListService | DAL | DbTokenCache.cs
// https://github.com/Azure-Samples/active-directory-dotnet-webapp-webapi-openidconnect.git | TodoListWebApp | Utils | NaiveSessionCache.cs

namespace MyUsrn.Dnc.Core
{

    public static class AzRedisTokenCache
    {
        static TokenCache userTokenCache;
        static TokenCacheNotificationArgs notificationArgs;

        //static readonly object lockObject = new object(); // not necessary in case of redis which handles multi-client access for you
        static string cacheId = string.Empty;
        static ConnectionMultiplexer connection = ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["CacheConnection"]);
        static IDatabase cache = connection.GetDatabase();
        //static Lazy<ConnectionMultiplexer> lazyConnection = new Lazy<ConnectionMultiplexer>(() => {
        //    return ConnectionMultiplexer.Connect(ConfigurationManager.AppSettings["CacheConnection"]);
        //});
        //ConnectionMultiplexer connection = lazyConnection.Value;

        /// <summary>
        /// Get the user token cache
        /// </summary>
        /// <returns></returns>
        public static TokenCache GetUserCache(string userId)
        {
            cacheId = userId;
            if (userTokenCache == null)
            {
                userTokenCache = new TokenCache();
                userTokenCache.SetBeforeAccess(BeforeAccessNotification);
                userTokenCache.SetAfterAccess(AfterAccessNotification);
            }
            //Load(new TokenCacheNotificationArgs().TokenCache = usertokenCache);
            return userTokenCache;
        }        

        public static void Load()
        {
            Debug.Assert(cache != null && cache.IsConnected(cacheId));

            //lock (lockObject)
            //{                
                var userIdTokenCache = cache.StringGet(cacheId);
                if (userIdTokenCache.HasValue)
                {
                //JsonConvert.DeserializeObject<AzRedisTokenCache>(userIdTokenCache);
                //notificationArgs.TokenCache.Deserialize(Encoding.UTF8.GetBytes(userIdTokenCache.ToString()));
                notificationArgs.TokenCache.Deserialize((byte[])userIdTokenCache);
#if DEBUG
                    var ttl = cache.KeyTimeToLive(cacheId);  // if null using default redis no expiry and lru
#endif
            }
            //}
        }

        public static void Persist()
        {
            Debug.Assert(cache != null && cache.IsConnected(cacheId));

            // reflect changes in the persistent store
            var cacheEntryExpiryDays = ConfigurationManager.AppSettings["CacheEntryExpiryDays"];
            int timeSpanFromDays = default(int);
            if (int.TryParse(cacheEntryExpiryDays, out timeSpanFromDays))
            {
                var expiry = TimeSpan.FromDays(timeSpanFromDays);
                //cache.StringSet(cacheId, JsonConvert.SerializeObject(notificationArgs.TokenCache), expiry);
                //cache.StringSet(cacheId, Encoding.UTF8.GetString(notificationArgs.TokenCache.Serialize()), expiry);
                cache.StringSet(cacheId, notificationArgs.TokenCache.Serialize(), expiry);
            }
            else
            {
                //cache.StringSet(cacheId, JsonConvert.SerializeObject(notificationArgs.TokenCache));
                //cache.StringSet(cacheId, Encoding.UTF8.GetString(notificationArgs.TokenCache.Serialize()));
                cache.StringSet(cacheId, notificationArgs.TokenCache.Serialize());
            }

#if DEBUG
            var ttl = cache.KeyTimeToLive(cacheId);  // if null using default redis no expiry and lru
#endif

            // once the write operation took place, restore the HasStateChanged bit to false
            //notificationArgs.HasStateChanged = false;
        }

        // Empties the persistent store.
        public static void Clear()
        {
            //base.Clear(); -> userTokenCache.Clear(); ???
            cache.KeyDelete(cacheId);
        }

        // Deletes item from the persistent store.
        public static void DeleteItem(/* TokenCacheItem item */)
        {
            //base.DeleteItem(item); -> userTokenCache.DeleteItem(item); ???
            Persist(); 
        }

        // Triggered right before ADAL needs to access the cache.        
        public static void BeforeAccessNotification(TokenCacheNotificationArgs args)
        {
            if (notificationArgs == null) notificationArgs = args;

            // Reload the cache from the persistent store, if there is a case where it could have changed since the last access, e.g. like NaiveCache.cs
            Load();

            // or If its deemed more efficient check if in-memory and persistent store versions are the same and only reload from persistent store when 
            // that is not the case, e.g. DbTokenCache.cs
        }

        // Triggered right after ADAL accessed the cache.
        public static void AfterAccessNotification(TokenCacheNotificationArgs args)
        {
            if (notificationArgs == null) notificationArgs = args;

            // if the access operation resulted in a cache update
            if (args.TokenCache.HasStateChanged)
            {
                Persist();
            }
        }
    }
}