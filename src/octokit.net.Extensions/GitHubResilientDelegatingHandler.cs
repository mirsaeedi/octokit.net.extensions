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

            // cannot used the cancelationToken because its timeout is preconfigured to 100 seconds by Octokit
            var httpResponse = await base.SendAsync(request, CancellationToken.None)
                .ConfigureAwait(false);

            _logger?.LogInformation("Response Recieved. Status Code: {statusCode}",httpResponse.StatusCode.ToString());


            return httpResponse;
        }
    }
}
