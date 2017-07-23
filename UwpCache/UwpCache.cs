using JsonNet.PrivateSettersContractResolvers;
using Newtonsoft.Json;
using NeoSmart.Utils;
using System;
using System.Linq;
using System.Threading.Tasks;
using Windows.Storage;
using System.Collections.Generic;
using System.Diagnostics;

namespace NeoSmart.UwpCache
{
    public static class Cache
    {
        private const string CacheFolderName = "$UwpCache$";
        public static Task<StorageFolder> CacheFolder = ApplicationData.Current.LocalCacheFolder.CreateFolderAsync(CacheFolderName, CreationCollisionOption.OpenIfExists).AsTask();
        public static TimeSpan DefaultLifetime = TimeSpan.FromDays(7);

        /// <summary>
        /// Sets the style used to convert keys to file names on-disk. Be careful to use only legal filename characters if KeyStyle is set to PlainText.
        /// </summary>
        public static KeyStyle FileNameStyle { get; set; } = KeyStyle.Hashed;

        private static char[] IllegalCharacters;

        static Cache()
        {
            var illegalChars = new List<char>(System.IO.Path.GetInvalidFileNameChars());
            illegalChars.Sort();
            IllegalCharacters = illegalChars.ToArray();
        }

        private struct StorageTemplate<T>
        {
            public DateTimeOffset Expiry;
            public T Value;
        }

        private static string Hash(string key)
        {
            switch (FileNameStyle)
            {
                case KeyStyle.Hashed:
                {
                    var hash = Farmhash.Sharp.Farmhash.Hash64(key);
                    var bytes = BitConverter.GetBytes(hash);
                    return UrlBase64.Encode(bytes);
                }
                case KeyStyle.Base64:
                {
                    var bytes = System.Text.Encoding.UTF8.GetBytes(key);
                    return UrlBase64.Encode(bytes);
                }
                case KeyStyle.PlainText:
                {
                    if (key.ToCharArray().Any(c => Array.BinarySearch(IllegalCharacters, c) >= 0))
                    {
                        throw new IllegalKeyException();
                    }
                    return key;
                }
            }

            throw new Exception("Invalid key hashing style set!");
        }

        /// <summary>
        /// Clears the cache of all items.
        /// </summary>
        /// <returns></returns>
        public static async Task Clear()
        {
            foreach (var child in await (await CacheFolder).GetItemsAsync())
            {
                await child.DeleteAsync();
            }
        }

        private static async Task<(bool Found, T Result)> TryGetHashAsync<T>(string keyHash)
        {
            var filename = $"{keyHash}.json";

            var file = (StorageFile)await (await CacheFolder).TryGetItemAsync(filename);
            if (file != null)
            {
                var json = await FileIO.ReadTextAsync(file);
                if (string.IsNullOrWhiteSpace(json))
                {
                    //this shouldn't be happening.
                    //even a cached null value should have an expiry parameter there

                    Debug.Fail("found empty cache file on disk!");
                    throw new Exception("Internal UwpCache exception: empty cache file found on disk!");
                }

                var settings = new JsonSerializerSettings
                {
                    ContractResolver = new PrivateSetterContractResolver()
                };
                var result = JsonConvert.DeserializeObject<StorageTemplate<T>>(json, settings);

                if (result.Expiry > DateTimeOffset.UtcNow)
                {
                    return (true, result.Value);
                }
            }

            return (false, default(T));
        }

        public static async Task<(bool Found, T Result)> TryGetAsync<T>(string key, Action<T> ifFound = null)
        {
            return await TryGetHashAsync<T>(Hash(key));
        }

        public static async Task<T> GetAsync<T>(string key)
        {
            return await GetAsync(key, (Func<T>)(() => throw new KeyNotFoundException(key)));
        }

        public static async Task<T> GetAsync<T>(string key, T result, TimeSpan expiry, bool cacheNull = false)
        {
            return await GetAsync(key, () => Task.FromResult(result), expiry, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, T result, DateTimeOffset? expiry = null, bool cacheNull = false)
        {
            return await GetAsync(key, () => Task.FromResult(result), expiry, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, Func<T> generator, TimeSpan expiry, bool cacheNull = false)
        {
            return await GetAsync(key, () => Task.FromResult(generator()), expiry, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, Func<T> generator, DateTimeOffset? expiry = null, bool cacheNull = false)
        {
            return await GetAsync(key, () => Task.FromResult(generator()), expiry, cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, Func<Task<T>> generator, TimeSpan expiry, bool cacheNull = false)
        {
            return await GetAsync(key, generator, DateTimeOffset.UtcNow.Add(expiry), cacheNull);
        }

        public static async Task<T> GetAsync<T>(string key, Func<Task<T>> generator, DateTimeOffset? expiry = null, bool cacheNull = false)
        {
            var hashed = Hash(key);
            var lookupResult = await TryGetHashAsync<T>(hashed);
            if (lookupResult.Found)
            {
                return lookupResult.Result;
            }

            //don't have or cannot use cached value
            var generated = await generator();
            if (generated != null || cacheNull)
            {
                await SetAsync(key, generated, expiry, cacheNull);
            }

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
            Debug.Assert(!string.IsNullOrWhiteSpace(serialized));
            var file = await (await CacheFolder).CreateFileAsync($"{hashed}.json", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(file, serialized);
        }
    }
}
