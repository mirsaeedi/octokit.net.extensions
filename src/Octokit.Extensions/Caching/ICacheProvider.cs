using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Octokit.Extensions
{
    public interface ICacheProvider
    {
        Task<CacheEntry> Get(CacheKey key);

        Task Add(CacheKey key, CacheEntry entry);

        Task Remove(CacheKey key);

        Task<bool> Exists(CacheKey key);

        Task ClearAll();
    }
}
