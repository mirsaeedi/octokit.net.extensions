using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using Polly;

namespace Octokit.Extensions
{
    public class ResilientGitHubClientFactory
    {
        private readonly ILogger _logger;

        public ResilientGitHubClientFactory(ILogger logger = null)
        {
            _logger = logger;
        }

        public GitHubClient Create(
            ProductHeaderValue productHeaderValue,
            Credentials credentials,
            ICacheProvider cacheProvider=null,
            Uri githubApiUrl = null,
            params IAsyncPolicy[] policies)
        {
            if (policies is null || policies.Length==0)
                policies = new ResilientPolicies(_logger).DefaultResilientPolicies; 

            var policy = policies.Length>1? Policy.WrapAsync(policies):policies[0];
            
            var githubConnection = new Connection(productHeaderValue,
               githubApiUrl ?? GitHubClient.GitHubApiUrl,
               new InMemoryCredentialStore(credentials),
               new HttpClientAdapter(() => GetHttpHandlerChain(_logger, policy, cacheProvider)),
               new SimpleJsonSerializer()
               );

            var githubClient = new GitHubClient(githubConnection);

            return githubClient;
        }

        private HttpMessageHandler GetHttpHandlerChain(ILogger logger, IAsyncPolicy policy, ICacheProvider cacheProvider)
        {
            var handler = HttpMessageHandlerFactory.CreateDefault();

            handler = new GitHubResilientHandler(handler, policy, _logger);

            if (cacheProvider != null)
            {
                handler = new HttpCacheHandler(handler,cacheProvider,logger); 
            }

            return handler;


        }

        public GitHubClient Create(
           ProductHeaderValue productHeaderValue,
           ICacheProvider cacheProvider = null,
           params IAsyncPolicy[] policies)
        {
            return Create(productHeaderValue, Credentials.Anonymous, cacheProvider, policies);
        }
    }
}
