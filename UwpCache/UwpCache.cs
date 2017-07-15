using JsonNet.PrivateSettersContractResolvers;
using Newtonsoft.Json;
using NeoSmart.Utils;
using System;
using System.Threading.Tasks;
using Windows.Storage;

namespace NeoSmart.UwpCache
{
    public static class Cache
    {
        public static string CacheFolderName = "$UwpCache$";
        private static StorageFolder CacheFolder;
        public static TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

        private struct StorageTemplate<T>
        {
            public DateTimeOffset Expiry;
            public T Value;
        }

        private static string Hash(string key)
        {
            var hash = Farmhash.Sharp.Farmhash.Hash64(key);
            var bytes = BitConverter.GetBytes(hash);
            return UrlBase64.Encode(bytes);
        }

        public static async Task Initialize()
        {
            if (CacheFolder == null)
            {
                CacheFolder = await ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(CacheFolderName, CreationCollisionOption.OpenIfExists);
            }
        }

        /// <summary>
        /// Clears the cache of all items.
        /// </summary>
        /// <returns></returns>
        public static async Task Clear()
        {
            await Initialize();
            foreach (var child in await CacheFolder.GetItemsAsync())
            {
                await child.DeleteAsync();
            }
        }

        public static async Task<T> GetAsync<T>(string key)
        {
            return await GetAsync(key, (Func<T>)(() => throw new KeyNotFoundException(key)));
        }

        public static async Task<T> GetAsync<T>(string key, T result, TimeSpan expiry, bool forceRefresh = false, bool cacheNull = false)
        {
            return await GetAsync(key, () => Task.FromResult(result), expiry, forceRefresh, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, T result, DateTimeOffset? expiry = null, bool forceRefresh = false, bool cacheNull = false)
        {
            return await GetAsync(key, () => Task.FromResult(result), expiry, forceRefresh, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, Func<T> generator, TimeSpan expiry, bool forceRefresh = false, bool cacheNull = false)
        {
            return await GetAsync(key, () => Task.FromResult(generator()), expiry, forceRefresh, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, Func<T> generator, DateTimeOffset? expiry = null, bool forceRefresh = false, bool cacheNull = false)
        {
            return await GetAsync(key, () => Task.FromResult(generator()), expiry, forceRefresh, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, Func<Task<T>> generator, TimeSpan expiry, bool forceRefresh = false, bool cacheNull = false)
        {
            return await GetAsync(key, generator, DateTimeOffset.UtcNow.Add(expiry), forceRefresh, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, Func<Task<T>> generator, DateTimeOffset? expiry = null, bool forceRefresh = false, bool cacheNull = false)
        {
            await Initialize();

            var hashed = Hash(key);
            var filename = $"{hashed}.json";

            var file = (StorageFile)await CacheFolder.TryGetItemAsync(filename);
            if (file != null)
            {
                var json = await FileIO.ReadTextAsync(file);

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new PrivateSetterContractResolver()
                };
                var result = JsonConvert.DeserializeObject<StorageTemplate<T>>(json, settings);

                if (result.Expiry > DateTimeOffset.UtcNow)
                {
                    return result.Value;
                }
            }

            //don't have or cannot use cached value
            var generated = await generator();
            await SetAsync(key, generated, expiry, cacheNull);

            return generated;
        }

        public static async Task SetAsync<T>(string key, Func<Task<T>> generator, TimeSpan expiry, bool cacheNull = false)
        {
            await SetAsync(key, await generator(), DateTimeOffset.UtcNow.Add(expiry), cacheNull);
        }

        public static async Task SetAsync<T>(string key, Func<Task<T>> generator, DateTimeOffset? expiry = null, bool cacheNull = false)
        {
            await SetAsync(key, await generator(), expiry, cacheNull);
        }

        public static async Task SetAsync<T>(string key, Func<T> generator, TimeSpan expiry, bool cacheNull = false)
        {
            await SetAsync(key, generator(), DateTimeOffset.UtcNow.Add(expiry), cacheNull);
        }

        public static async Task SetAsync<T>(string key, Func<T> generator, DateTimeOffset? expiry = null, bool cacheNull = false)
        {
            await SetAsync(key, generator(), expiry, cacheNull);
        }

        public static async Task SetAsync<T>(string key, T value, TimeSpan expiry, bool cacheNull = false)
        {
            await SetAsync(key, value, DateTimeOffset.UtcNow.Add(expiry), cacheNull);
        }

        public static async Task SetAsync<T>(string key, T value, DateTimeOffset? expiry = null, bool cacheNull = false)
        {
            await Initialize();

            if (!cacheNull && value == null)
            {
                return;
            }

            var cached = new StorageTemplate<T>
            {
                Expiry = expiry ?? DateTimeOffset.UtcNow.Add(DefaultLifetime),
                Value = value
            };

            var hashed = Hash(key);
            var serialized = JsonConvert.SerializeObject(cached);
            var file = await CacheFolder.CreateFileAsync($"{hashed}.json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, serialized);
        }
    }
}
