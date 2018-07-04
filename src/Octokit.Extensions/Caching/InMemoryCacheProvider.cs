using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Octokit.Extensions
{
    public class InMemoryCacheProvider : ICacheProvider
    {
        private readonly MemoryCache _cache;

        public InMemoryCacheProvider(MemoryCacheOptions memoryCacheOptions=null)
        {
            _cache= new MemoryCache(memoryCacheOptions?? new MemoryCacheOptions());
        }
        public async Task Add(CacheKey key, CacheEntry entry)
        {
            _cache.Set(key,entry);
        }

        public Task ClearAll()
        {
            throw new InvalidOperationException("You cannot clear the in-memory cache");
        }

        public async Task<bool> Exists(CacheKey key)
        {
           return _cache.TryGetValue(key,out _);
        }

        public async Task<CacheEntry> Get(CacheKey key)
        {
            return _cache.Get<CacheEntry>(key);
        }

        public async Task Remove(CacheKey key)
        {
            _cache.Remove(key);
        }
    }
}
