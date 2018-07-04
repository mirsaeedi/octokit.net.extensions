using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octokit.Extensions
{
    public class HttpCacheHandler : DelegatingHandler
    {
        private readonly ICacheProvider _cache;
        private readonly ILogger _logger;

        public HttpCacheHandler(HttpMessageHandler innerHandler, ICacheProvider cache, ILogger logger = null)
        {
            _cache = cache;
            InnerHandler = innerHandler;
            _logger = logger;
        }
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if(!RequestIsCachable(request))
                return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

            var existingResponseEntry = await GetResponseFromCache(request).ConfigureAwait(false);

            if (existingResponseEntry == null)
            {
                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
                await AddOrUpdateCache(request,response).ConfigureAwait(false);
                return response;
            }
            else
            {
                ApplyConditionalHeadersToRequest(request, existingResponseEntry);

                var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.NotModified)
                {
                    _logger?.LogInformation("Response returned from the cache. ETAG: {etag}, URI:{URI}",existingResponseEntry.ETag.Tag,
                        request.RequestUri.AbsolutePath.ToString());

                    return CacheEntry.CreateHttpResponseMessage(existingResponseEntry,response);
                }

                await AddOrUpdateCache(request, response).ConfigureAwait(false);

                return response;
            }
        }

        private async Task AddOrUpdateCache(HttpRequestMessage request, HttpResponseMessage response)
        {
            var primaryKey = new CacheKey(request.RequestUri);
            var entryExists = await _cache.Exists(primaryKey).ConfigureAwait(false);

            if (entryExists)
                await _cache.Remove(primaryKey).ConfigureAwait(false);

            await AddToCache(request,response).ConfigureAwait(false);
        }

        private bool RequestIsCachable(HttpRequestMessage request)
        {
            return request.Method == HttpMethod.Get;
        }

        private void ApplyConditionalHeadersToRequest(HttpRequestMessage request, CacheEntry entry)
        {
            if (entry == null || !entry.HasValidator) return;

            if (entry.ETag != null)
            {
                request.Headers.IfNoneMatch.Add(entry.ETag);
            }
            
            if(entry.LastModified!=null)
            {
                request.Headers.IfModifiedSince = entry.LastModified;
            }
        }

        private async Task AddToCache(HttpRequestMessage request, HttpResponseMessage response)
        {
            var primaryKey = new CacheKey(request.RequestUri);
            var entry = await CacheEntry.Create(response).ConfigureAwait(false);
            await _cache.Add(primaryKey, entry).ConfigureAwait(false);
        }

        private async Task<CacheEntry> GetResponseFromCache(HttpRequestMessage request)
        {
            var primaryKey = new CacheKey(request.RequestUri);
            return await _cache.Get(primaryKey);
        }
    }
}
