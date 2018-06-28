using System;

namespace Octokit.Extensions.Tests.Integration
{
    public static class Helper
    {
        static readonly Lazy<Credentials> _credentialsThunk = new Lazy<Credentials>(() =>
        {
            var githubToken = Environment.GetEnvironmentVariable("OCTOKIT_OAUTHTOKEN");

            if (githubToken != null)
                return new Credentials(githubToken);

            var githubUsername = Environment.GetEnvironmentVariable("OCTOKIT_GITHUBUSERNAME");
            var githubPassword = Environment.GetEnvironmentVariable("OCTOKIT_GITHUBPASSWORD");

            if (githubUsername == null || githubPassword == null)
                return null;

            return new Credentials(githubUsername, githubPassword);
        });

        public static Credentials Credentials { get { return _credentialsThunk.Value; } }
    }
}
