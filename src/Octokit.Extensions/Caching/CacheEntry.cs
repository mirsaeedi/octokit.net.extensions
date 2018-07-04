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
        HttpStatusCode StatusCode { get; set; }
        public CacheControlHeaderValue CacheControl { get; set; }
        public bool HasValidator => ETag != null || (Content != null && LastModified != null);
        public DateTimeOffset? LastModified { get; set; }
        public EntityTagHeaderValue ETag { get; internal set; }
        byte[] Content { get; set; }

        Dictionary<string, IEnumerable<string>> ContentHeaders { get; set; } = new Dictionary<string, IEnumerable<string>>();

        public static async Task<CacheEntry> Create(HttpResponseMessage response)
        {
            var cacheEntry = new CacheEntry();
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
