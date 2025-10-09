using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace VladiCore.Api.Infrastructure;

public interface ICacheProvider
{
    T GetOrCreate<T>(string key, TimeSpan ttl, Func<T> factory);

    Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory);

    void Remove(string key);

    void RemoveByPrefix(string prefix);
}

public class MemoryCacheProvider : ICacheProvider
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new ConcurrentDictionary<string, byte>(StringComparer.OrdinalIgnoreCase);

    public MemoryCacheProvider(IMemoryCache cache)
    {
        _cache = cache;
    }

    public T GetOrCreate<T>(string key, TimeSpan ttl, Func<T> factory)
    {
        if (_cache.TryGetValue(key, out T? value))
        {
            return value;
        }

        value = factory();
        _cache.Set(key, value, ttl);
        _keys[key] = 0;
        return value;
    }

    public async Task<T> GetOrCreateAsync<T>(string key, TimeSpan ttl, Func<Task<T>> factory)
    {
        if (_cache.TryGetValue(key, out T? value))
        {
            return value;
        }

        value = await factory().ConfigureAwait(false);
        _cache.Set(key, value, ttl);
        _keys[key] = 0;
        return value;
    }

    public void Remove(string key)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
    }

    public void RemoveByPrefix(string prefix)
    {
        foreach (var key in _keys.Keys)
        {
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                Remove(key);
            }
        }
    }
}
