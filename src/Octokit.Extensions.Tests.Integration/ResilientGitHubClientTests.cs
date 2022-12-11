using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Octokit.Extensions.Tests.Integration
{
    public class ResilientGitHubClientTests
    {
        [Fact]
        public async Task MakesWrappedOctokitRequest()
        {
            var credentials = Helper.Credentials;

            var client = new ResilientGitHubClientFactory()
                .Create(new ProductHeaderValue("Octokit.Extensions.Tests"), credentials);

            var repo = await client.Repository.Get("octokit", "octokit.net");

            Assert.Equal("octokit", repo.Owner.Login);
            Assert.Equal("octokit.net", repo.Name);
        }

        [Fact(Skip = "This seems to fail. Needs investigating.")]
        public async Task MakesCachedWrappedOctokitRequest()
        {
            var credentials = Helper.Credentials;

            var client = new ResilientGitHubClientFactory()
                .Create(new ProductHeaderValue("Octokit.Extensions.Tests"), credentials,new InMemoryCacheProvider(),new ResilientPolicies().DefaultResilientPolicies);

            var repo = await client.Repository.Get("octokit", "octokit.net");
            var remaining = client.GetLastApiInfo().RateLimit.Remaining;
            var cachedRepo = await client.Repository.Get("octokit", "octokit.net");
            Assert.Equal(client.GetLastApiInfo().RateLimit.Remaining, remaining);
            var cachedRepo2 = await client.Repository.Get("octokit", "octokit.net");
            Assert.Equal(client.GetLastApiInfo().RateLimit.Remaining, remaining);

            Assert.Equal("octokit", cachedRepo.Owner.Login);
            Assert.Equal("octokit.net", cachedRepo2.Name);
        }

        [Fact]
        public async Task MakesCachedWrappedOctokitRequest2()
        {
            var client = new ResilientGitHubClientFactory()
                .Create(new ProductHeaderValue("Octokit.Extensions.Tests"), new InMemoryCacheProvider());

            var owner = "dotnet";
            var repo = "roslyn";
            var pullRequestNumber = 28263;

            var githubReviews = (await client
                        .PullRequest
                        .Review
                        .GetAll(owner, repo, pullRequestNumber, new ApiOptions() { PageSize = 1000 }))
                        .ToArray();

            var cachedGithubReviews = (await client
                        .PullRequest
                        .Review
                        .GetAll(owner, repo, pullRequestNumber, new ApiOptions() { PageSize = 1000 }))
                        .ToArray();


            Assert.Equal(githubReviews.Length,cachedGithubReviews.Length);

        }


    }
}
