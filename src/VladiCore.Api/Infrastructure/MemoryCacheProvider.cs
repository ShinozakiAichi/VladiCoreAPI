using System;
using System.Collections.Generic;
using System.Runtime.Caching;

namespace VladiCore.Api.Infrastructure
{
    public interface ICacheProvider
    {
        T GetOrCreate<T>(string key, TimeSpan ttl, Func<T> factory);
        void Remove(string key);
        void RemoveByPrefix(string prefix);
    }

    public class MemoryCacheProvider : ICacheProvider
    {
        private readonly ObjectCache _cache = MemoryCache.Default;

        public T GetOrCreate<T>(string key, TimeSpan ttl, Func<T> factory)
        {
            if (_cache.Contains(key))
            {
                return (T)_cache[key];
            }

            var value = factory();
            _cache.Set(key, value, DateTimeOffset.UtcNow.Add(ttl));
            return value;
        }

        public void Remove(string key)
        {
            if (_cache.Contains(key))
            {
                _cache.Remove(key);
            }
        }

        public void RemoveByPrefix(string prefix)
        {
            var keys = new List<string>();
            foreach (var item in _cache)
            {
                if (item.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    keys.Add(item.Key);
                }
            }

            foreach (var key in keys)
            {
                _cache.Remove(key);
            }
        }
    }
}
