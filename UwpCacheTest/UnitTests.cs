using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NeoSmart.UwpCache;
using System.Threading.Tasks;

namespace UwpCacheTest
{
    [TestClass]
    public class CacheTests
    {
        public static string NewKey => Guid.NewGuid().ToString();

        private void Run(Func<Task> callback)
        {
            callback().GetAwaiter().GetResult();
        }

        [TestMethod]
        public void GetUnset()
        {
            Run(async () =>
            {
                await Cache.ClearAsync();
                await Assert.ThrowsExceptionAsync<KeyNotFoundException>(async () => await Cache.GetAsync<object>(NewKey), "Unexpectedly found object in cleared cache!");
            });
        }

        [TestMethod]
        public void ClearCache()
        {
            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, 42);
                await Cache.ClearAsync();
                await Assert.ThrowsExceptionAsync<KeyNotFoundException>(async () => await Cache.GetAsync<object>(key), "Unexpectedly found object in cleared cache!");
            });
        }

        [TestMethod]
        public void GetFixedValue()
        {
            //test the ability to pass in a value, not a generator
            Run(async () =>
            {
                Assert.AreEqual(await Cache.GetAsync(NewKey, 42), 42);
            });
        }

        [TestMethod]
        public void GetSyncDynamicValue()
        {
            //test the ability to pass in a non-async generator
            Run(async () =>
            {
                Assert.AreEqual(await Cache.GetAsync(NewKey, () => 42), 42);
            });
        }

        [TestMethod]
        public void GetAsyncDynamicValue()
        {
            //test the ability to pass in a non-async generator
            Run(async () =>
            {
                Assert.AreEqual(await Cache.GetAsync(NewKey, async () => await Task.FromResult(42)), 42);
            });
        }

        [TestMethod]
        public void SetFixedValue()
        {
            //test the ability to pass in a value, not a generator
            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, 42);
                Assert.AreEqual(await Cache.GetAsync<int>(key), 42);
            });
        }

        [TestMethod]
        public void SetSyncDynamicValue()
        {
            //test the ability to pass in a non-async generator
            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, () => 42);
                Assert.AreEqual(await Cache.GetAsync<int>(key), 42);
            });
        }

        [TestMethod]
        public void SetAsyncDynamicValue()
        {
            //test the ability to pass in a non-async generator
            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, async () => await Task.FromResult(42));
                Assert.AreEqual(await Cache.GetAsync<int>(key), 42);
            });
        }

        [TestMethod]
        public void TimeSpanExpiry()
        {
            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, 42, TimeSpan.FromMilliseconds(50));
                Assert.AreEqual(await Cache.GetAsync<int>(key), 42);
                await Task.Delay(75);
                await Assert.ThrowsExceptionAsync<KeyNotFoundException>(async () => await Cache.GetAsync<int>(key), "Item not evicted from cache after delay!");
            });
        }

        [TestMethod]
        public void DateTimeOffsetExpiry()
        {
            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, 42, DateTime.UtcNow.AddMilliseconds(50));
                Assert.AreEqual(await Cache.GetAsync<int>(key), 42);
                await Task.Delay(75);
                await Assert.ThrowsExceptionAsync<KeyNotFoundException>(async () => await Cache.GetAsync<int>(key), "Item not evicted from cache after delay!");
            });
        }

        [TestMethod]
        public void NullNotCached()
        {
            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, async () => await Task.FromResult<int?>(null));
                await Assert.ThrowsExceptionAsync<KeyNotFoundException>(async () => await Cache.GetAsync<int?>(key), "Null value cached!");
            });
        }

        [TestMethod]
        public void NullCached()
        {
            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, async () => await Task.FromResult<int?>(null), cacheNull: true);
                Assert.AreEqual(await Cache.GetAsync<int?>(key), null);
            });
        }

        [TestMethod]
        public void PlainTextKeyValidation()
        {
            var oldStyle = Cache.FileNameStyle;
            Cache.FileNameStyle = KeyStyle.PlainText;

            var illegalChars = System.IO.Path.GetInvalidFileNameChars();
            if (illegalChars.Length == 0)
            {
                Assert.Fail("Cannot test plain text key validation on this platform.");
                return;
            }

            Run(async () =>
            {
                var key = NewKey;
                await Cache.SetAsync(key, 42);
                await Cache.GetAsync<int>(key);
                await Assert.ThrowsExceptionAsync<IllegalKeyException>(async () => await Cache.GetAsync<int>($"{illegalChars[0]}"));
            });

            Cache.FileNameStyle = oldStyle;
        }
    }
}
