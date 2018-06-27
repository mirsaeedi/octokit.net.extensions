# oktokit.net.Extensions
This library extends octokit.net, the well-known GitHub client, by enriching it with resilient connections and logging capabilities. This project hosts the awesome [Polly](https://github.com/App-vNext/Polly) at its heart. Using octokit.net.Extension you get the following out of the box:

## Resiliency against sudden and random http failures
In case of happening _HttpRequestException_, the built-in policy tries to prevent the whole process from stopping by resending the request according to the following policy.

```C#
public  Policy DefaultHttpRequestExceptionPolicy => Policy.Handle<HttpRequestException>()
.WaitAndRetryForeverAsync(sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
onRetry: (exception, timespan) =>
{
_logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds","HttpRequestException",timespan.TotalSeconds);
});
```

## Resiliency against http timeouts
In case of happening timout exceptions which been carrying in _TaskCanceledException_ exception, the built-in policy tries to prevent the whole process from stopping by resending the request according to the following policy.

```C#
public Policy DefaultTimeoutExceptionPolicy => Policy.Handle<TaskCanceledException>(ex => !ex.CancellationToken.IsCancellationRequested)
.WaitAndRetryForeverAsync(sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
onRetry: (exception, timespan) =>
{
   _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "TaskCanceledException", timespan.TotalSeconds);
});
```
## Resiliency against exceeeding GitHub's rate limit policy
In case of reaching the Github's [rate limit](), octokit throws _RateLimitExceededException_ exception. To deal with the situation, the built-in policy tries to prevent the whole process from stopping by waiting untill the rate limit window expires. The default implemented policy is as follows

```C#
public Policy DefaultRateLimitExceededExceptionPolicy => Policy.Handle<RateLimitExceededException>()
.RetryAsync(retryCount: 1,onRetry: async (exception, retryCount) =>
{
  var e = exception as RateLimitExceededException;

  var sleepMilliseconds = (int)(e.Reset.ToLocalTime() - DateTime.Now).TotalMilliseconds;

  _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "RateLimitExceededException", sleepMilliseconds/1000);

  await Task.Delay(sleepMilliseconds < 0 ? 10 : sleepMilliseconds + 1000).ConfigureAwait(false);
});
```
## Resiliency against GitHub's abuse policy
In case of [abusing]() the Github policy, octokit throws _AbuseException_ exception. To deal with the situation, the built-in policy tries to prevent the whole process from stopping by waiting untill the abuse time window expires. The default implemented policy is as follows

```C#
public Policy DefaultAbuseExceptionExceptionPolicy => Policy.Handle<AbuseException>()
.RetryAsync(retryCount: 1,onRetry: async (exception, retryCount) =>
{
  var e = exception as AbuseException;

  var sleepMilliseconds = (int)TimeSpan.FromSeconds(e.RetryAfterSeconds.GetValueOrDefault(30)).TotalMilliseconds;

  _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "AbuseException", sleepMilliseconds / 1000);

  await Task.Delay(sleepMilliseconds).ConfigureAwait(false);
});
```
