using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace Octokit.Extensions
{
    public class CacheKey
    {
        private readonly Uri _uri;
        private readonly HttpMethod _method;


        public CacheKey(Uri uri)
        {
            _uri = uri;

            // we only cache Get requests
            _method = HttpMethod.Get; 
        }

        public override bool Equals(object obj)
        {
            var key2 = (CacheKey)obj;
            return key2._uri == _uri && key2._method == _method;
        }

        public override int GetHashCode()
        {
            int hash = 13;
            hash = (hash * 7) + _uri.GetHashCode();
            hash = (hash * 7) + _method.GetHashCode();
            return hash;
        }
    }
}
