using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Octokit.Extensions
{
    public class CacheEntry
    {
        public CacheKey Key { get; internal set; }
        public HttpStatusCode StatusCode { get; internal set; }
        public bool HasValidator => ETag != null || (Content != null && LastModified != null);
        public DateTimeOffset? LastModified { get; internal set; }
        public EntityTagHeaderValue ETag { get; internal set; }
        public byte[] Content { get; internal set; }
        public Dictionary<string, IEnumerable<string>> ContentHeaders { get; internal set; } = new Dictionary<string, IEnumerable<string>>();

        public CacheEntry(CacheKey key,HttpStatusCode statusCode,DateTimeOffset? lastModified,EntityTagHeaderValue etag,byte[] content,
            Dictionary<string, IEnumerable<string>> contentHeaders)
        {
            Key = key;
            StatusCode = statusCode;
            LastModified = lastModified;
            ETag = etag;
            Content = content;
            ContentHeaders = contentHeaders;
        }

        private CacheEntry()
        {

        }

        public static async Task<CacheEntry> Create(HttpResponseMessage response)
        {
            var cacheEntry = new CacheEntry();
            cacheEntry.Key = new CacheKey(response.RequestMessage.RequestUri);
            cacheEntry.StatusCode = response.StatusCode;

            cacheEntry.ETag = response.Headers?.ETag;
            cacheEntry.LastModified = response.Content?.Headers?.LastModified;

            await FillContent(response, cacheEntry).ConfigureAwait(false);
            FillContentHeader(response,cacheEntry);

            return cacheEntry;
        }

        private static void FillContentHeader(HttpResponseMessage response, CacheEntry cacheEntry)
        {
            if (response.Content == null)
                return;

            foreach (var header in response.Content.Headers)
            {
                cacheEntry.ContentHeaders[header.Key] = header.Value;
            }
        }

        private static async Task FillContent(HttpResponseMessage response,CacheEntry entry)
        {
            if (response.Content == null)
                return;

            await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);

            var ms = new MemoryStream();
            await response.Content.CopyToAsync(ms).ConfigureAwait(false);
            ms.Position = 0;

            entry.Content = ms.ToArray();
        }

        internal static HttpResponseMessage CreateHttpResponseMessage(CacheEntry entry, HttpResponseMessage response)
        {
            var newResponse = new HttpResponseMessage(entry.StatusCode);
            
            foreach (var v in response.Headers)
                newResponse.Headers.TryAddWithoutValidation(v.Key, v.Value);

            if (entry.Content != null)
            {
                var ms = new MemoryStream(entry.Content);
                newResponse.Content = new StreamContent(ms);

                foreach (var v in entry.ContentHeaders)
                    newResponse.Content.Headers.TryAddWithoutValidation(v.Key, v.Value);
            }

            return newResponse;
        }

    }
}
