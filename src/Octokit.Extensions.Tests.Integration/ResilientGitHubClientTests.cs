using System.Threading.Tasks;
using octokit.net.Extensions;
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
    }
}
