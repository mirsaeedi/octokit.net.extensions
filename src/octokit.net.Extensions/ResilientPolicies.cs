using Microsoft.Extensions.Logging;
using Octokit;
using Polly;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace octokit.net.Extensions
{
    public class ResilientPolicies
    {
        private readonly ILogger _logger;

        public ResilientPolicies(ILogger logger=null)
        {
            _logger = logger;
        }
        public  Policy DefaultHttpRequestExceptionPolicy => Policy.Handle<HttpRequestException>()
            .WaitAndRetryForeverAsync(
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timespan) =>
            {
                _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "HttpRequestException",timespan.TotalSeconds);
            });

        public Policy DefaultTimeoutExceptionPolicy => Policy.Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
            .WaitAndRetryForeverAsync(
            sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
            onRetry: (exception, timespan) =>
            {
                _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "TaskCanceledException", timespan.TotalSeconds);
            });

        public Policy DefaultRateLimitExceededExceptionPolicy => Policy.Handle<RateLimitExceededException>()
            .RetryAsync(
            retryCount: 1,
            onRetryAsync: async (exception, retryCount) =>
            {
                var e = exception as RateLimitExceededException;

                var sleepMilliseconds = (int)(e.Reset.ToLocalTime() - DateTime.Now)
                    .TotalMilliseconds;

                _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "RateLimitExceededException", sleepMilliseconds/1000);

                await Task.Delay(sleepMilliseconds < 0 ? 10 : sleepMilliseconds + 1000).ConfigureAwait(false);
            });

        public Policy DefaultAbuseExceptionExceptionPolicy => Policy.Handle<AbuseException>()
           .RetryAsync(
            retryCount: 1,
            onRetryAsync: async (exception, retryCount) =>
            {
                var e = exception as AbuseException;

                var sleepMilliseconds = (int)TimeSpan.FromSeconds(e.RetryAfterSeconds.GetValueOrDefault(30))
                    .TotalMilliseconds;

                _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "AbuseException", sleepMilliseconds / 1000);

                await Task.Delay(sleepMilliseconds)
                .ConfigureAwait(false);
            });

        public IAsyncPolicy[] DefaultResilientPolicies => new IAsyncPolicy[]{DefaultHttpRequestExceptionPolicy,
                DefaultRateLimitExceededExceptionPolicy,
                DefaultAbuseExceptionExceptionPolicy,
                DefaultTimeoutExceptionPolicy };
    }
}
