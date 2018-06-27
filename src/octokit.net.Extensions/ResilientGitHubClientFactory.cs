using Microsoft.Extensions.Logging;
using Octokit;
using Octokit.Internal;
using Polly;
using System;
using System.Collections.Generic;
using System.Text;

namespace octokit.net.Extensions
{
    public class ResilientGitHubClientFactory
    {
        private readonly ILogger _logger;
        public ResilientGitHubClientFactory(ILogger logger=null)
        {
            _logger = logger;
        }

        public GitHubClient Create(
            ProductHeaderValue productHeaderValue,
            ICredentialStore credentialStore,
            params IAsyncPolicy[] policies)
        {

            var policy = Policy.WrapAsync(policies);
            
            var githubConnection = new Connection(productHeaderValue,
               GitHubClient.GitHubApiUrl,
               credentialStore,
               new HttpClientAdapter(() =>
               new GitHubResilientDelegatingHandler(policy,_logger)
               {
                   InnerHandler = HttpMessageHandlerFactory.CreateDefault()
               }),
               new SimpleJsonSerializer()
               );

            return new GitHubClient(githubConnection);
        }

        public GitHubClient Create(
           ProductHeaderValue productHeaderValue,
           params IAsyncPolicy[] policies)
        {

            var policy = Policy.WrapAsync(policies);

            var githubConnection = new Connection(productHeaderValue,
               GitHubClient.GitHubApiUrl,
               new InMemoryCredentialStore(Credentials.Anonymous),
               new HttpClientAdapter(() => new GitHubResilientDelegatingHandler(policy, _logger)
               {
                   InnerHandler = HttpMessageHandlerFactory.CreateDefault()
               }),
               new SimpleJsonSerializer()
               );

            return new GitHubClient(githubConnection);
        }
    }
}
