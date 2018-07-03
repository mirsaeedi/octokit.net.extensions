using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Octokit.Extensions
{
    class GitHubResilientHandler : DelegatingHandler
    {
        // we need this instance to be able to call Octokit's internal code using reflection
        private static Lazy<HttpClientAdapter> _httpClientAdapter = new Lazy<HttpClientAdapter>(()=> 
            new HttpClientAdapter(()=>new GitHubResilientHandler(null, null)), true);

        private readonly IAsyncPolicy _policy;
        private readonly ILogger _logger;

        public GitHubResilientHandler(IAsyncPolicy policy,ILogger logger=null)
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
            var httpResponse = await base.SendAsync(request, CancellationToken.None).ConfigureAwait(false);

            _logger?.LogInformation("Response Recieved. Status Code: {statusCode}",httpResponse.StatusCode.ToString());

            var githubResponse = await GetGitHubResponse(httpResponse).ConfigureAwait(false);
           
            _logger?.LogInformation("Remaining Limit: {remaining} - Reset At: {reset}",
                (int)githubResponse.ApiInfo.RateLimit.Remaining,
                (DateTime)githubResponse.ApiInfo.RateLimit.Reset.ToLocalTime());

            TryToThrowGitHubRelatedErrors(githubResponse);

            return httpResponse;
        }

        private void TryToThrowGitHubRelatedErrors(dynamic githubResponse)
        {
            MethodInfo handleErrors = typeof(Connection).GetMethod("HandleErrors", BindingFlags.NonPublic | BindingFlags.Static);

            try
            {
                handleErrors.Invoke(this, new object[] { githubResponse });
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }

        private async Task<dynamic> GetGitHubResponse(HttpResponseMessage httpResponse)
        {

            MethodInfo buildResponseMethod = typeof(HttpClientAdapter).GetMethod("BuildResponse", BindingFlags.NonPublic | BindingFlags.Instance);

            var clonedHttpResponse = await CloneResponseAsync(httpResponse).ConfigureAwait(false);

            var githubResponse = await(dynamic) buildResponseMethod.Invoke(_httpClientAdapter.Value, new object[] { clonedHttpResponse });

            return githubResponse;
        }

        private async Task<HttpResponseMessage> CloneResponseAsync(HttpResponseMessage response)
        {
            var newResponse = new HttpResponseMessage(response.StatusCode);
            var ms = new MemoryStream();

            foreach (var v in response.Headers) newResponse.Headers.TryAddWithoutValidation(v.Key, v.Value);

            if (response.Content != null)
            {
                // need to call LoadIntoBuffer, otherwise Octokit complains that it can't read the stream
                await response.Content.LoadIntoBufferAsync().ConfigureAwait(false);
                await response.Content.CopyToAsync(ms).ConfigureAwait(false);

                ms.Position = 0;
                newResponse.Content = new StreamContent(ms);
                foreach (var v in response.Content.Headers) newResponse.Content.Headers.TryAddWithoutValidation(v.Key, v.Value);
                
            }

            return newResponse;
        }
    }
}
