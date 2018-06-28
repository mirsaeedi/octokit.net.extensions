using Microsoft.Extensions.Logging;
using Octokit;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace octokit.net.Extensions
{
    class GitHubResilientDelegatingHandler : DelegatingHandler
    {
        private readonly IAsyncPolicy _policy;
        private readonly ILogger _logger;

        public GitHubResilientDelegatingHandler(IAsyncPolicy policy,ILogger logger=null)
        {
            _policy = policy;
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (_policy == null)
            {
                throw new ArgumentNullException(nameof(_policy));
            }

            var httpResponse = await _policy.ExecuteAsync(async () => await SendCoreAsync(request, cancellationToken))
                .ConfigureAwait(false);

            return httpResponse;
        }

        private async Task<HttpResponseMessage> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _logger?.LogInformation("Sending Request: {requestMethod} - {requestUrl}"
                ,request.Method.Method,request.RequestUri.ToString());

            // cannot use the cancelationToken because its timeout is preconfigured to 100 seconds by Octokit
            var httpResponse = await base.SendAsync(request, CancellationToken.None)
                .ConfigureAwait(false);

            _logger?.LogInformation("Response Recieved. Status Code: {statusCode}",httpResponse.StatusCode.ToString());

            var githubRespone = await BuildResponse(httpResponse);

            _logger?.LogInformation("Remaining Limit: {remaining} - Reset At: {reset}",
                githubRespone.ApiInfo.RateLimit.Remaining,
                githubRespone.ApiInfo.RateLimit.Reset.ToLocalTime());

            MethodInfo handleErrors = typeof(Connection)
                .GetMethod("HandleErrors",
                BindingFlags.NonPublic | BindingFlags.Static);

            try
            {
                handleErrors.Invoke(this, new object[] { githubRespone });
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }


            return httpResponse;
        }

        protected virtual async Task<IResponse> BuildResponse(HttpResponseMessage responseMessage)
        {
            Ensure.ArgumentNotNull(responseMessage, nameof(responseMessage));

            object responseBody = null;
            string contentType = null;

            // We added support for downloading images,zip-files and application/octet-stream. 
            // Let's constrain this appropriately.
            var binaryContentTypes = new[] {
                "application/zip" ,
                "application/x-gzip" ,
                "application/octet-stream"};

            var content = responseMessage.Content;

            if (content != null)
            {
                contentType = GetContentMediaType(responseMessage.Content);

                if (contentType != null && (contentType.StartsWith("image/") || binaryContentTypes
                    .Any(item => item.Equals(contentType, StringComparison.OrdinalIgnoreCase))))
                {
                    responseBody = await responseMessage.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                }
                else
                {
                    responseBody = await responseMessage.Content.ReadAsStringAsync().ConfigureAwait(false);
                }
            }

            return new Response(
                responseMessage.StatusCode,
                responseBody,
                responseMessage.Headers.ToDictionary(h => h.Key, h => h.Value.First()),
                contentType);
        }

        static string GetContentMediaType(HttpContent httpContent)
        {
            if (httpContent.Headers != null && httpContent.Headers.ContentType != null)
            {
                return httpContent.Headers.ContentType.MediaType;
            }
            return null;
        }
    }
}
