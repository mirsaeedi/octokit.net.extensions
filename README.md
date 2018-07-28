# oktokit.Extensions
This library extends octokit.net, the well-known GitHub client, by enriching it with **caching**, **resilient connections** and **logging** capabilities. In fact, using this library your process won't halt in case of happening exceptions such as Rate Limit, Abuse, Http Exceptions and you will consume your rate limit more wisely. 

Octokit.Extension adds a middleware to the Octokit's _HttpClient_ and will try to resend the requests if anything goes wrong. There are some built-in policies that define how Octokit.Extensions should act to handle the exceptions. However, you can easily define your own custom policies to deal with exceptions based on your requirements.

This project hosts the awesome [Polly](https://github.com/App-vNext/Polly) at its heart. We are using Polly to define policies and to incorporate the _retry_ behaviour into Octokit.Extensions. Using this library you get the following policies and behavior out of the box.

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
#Policies

All the default policies are implemented inside [ResilientPolicies](https://github.com/mirsaeedi/octokit.net.extensions/blob/master/src/Octokit.Extensions/Resiliency/ResilientPolicies.cs) class. 

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
In case of reaching the Github's [rate limit](https://developer.github.com/v3/#rate-limiting), octokit throws _RateLimitExceededException_ exception. To deal with the situation, the built-in policy tries to prevent the whole process from stopping by waiting untill the rate limit window expires. The default implemented policy is as follows

```C#
public Policy DefaultRateLimitExceededExceptionPolicy => Policy.Handle<RateLimitExceededException>()
.RetryAsync(retryCount: 1,onRetryAsync: async (exception, retryCount) =>
{
  var e = exception as RateLimitExceededException;

  var sleepMilliseconds = (int)(e.Reset.ToLocalTime() - DateTime.Now).TotalMilliseconds;

  _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "RateLimitExceededException", sleepMilliseconds/1000);

  await Task.Delay(sleepMilliseconds < 0 ? 10 : sleepMilliseconds + 1000).ConfigureAwait(false);
});
```
## Resiliency against GitHub's abuse policy
In case of [abusing](https://developer.github.com/v3/guides/best-practices-for-integrators/#dealing-with-abuse-rate-limits) the Github policy, octokit throws _AbuseException_ exception. To deal with the situation, the built-in policy tries to prevent the whole process from stopping by waiting untill the abuse time window expires. The default implemented policy is as follows

```C#
public Policy DefaultAbuseExceptionExceptionPolicy => Policy.Handle<AbuseException>()
.RetryAsync(retryCount: 1,onRetryAsync: async (exception, retryCount) =>
{
  var e = exception as AbuseException;

  var sleepMilliseconds = (int)TimeSpan.FromSeconds(e.RetryAfterSeconds.GetValueOrDefault(30)).TotalMilliseconds;

  _logger?.LogInformation("A {exception} has occurred. Next try will happen in {time} seconds", "AbuseException", sleepMilliseconds / 1000);

  await Task.Delay(sleepMilliseconds).ConfigureAwait(false);
});
```
## Caching

You can easily opt-in your cache provider of choice by implementing [ICacheProvider](https://github.com/mirsaeedi/octokit.net.extensions/blob/master/src/Octokit.Extensions/Caching/ICacheProvider.cs) interface. Also, there is an [in-memory](https://github.com/mirsaeedi/octokit.net.extensions/blob/master/src/Octokit.Extensions/Caching/InMemoryCacheProvider.cs) built-in cache provider available to you.


```C#

var credentials = new Octokit.Credentials(token);

// with default in-memory caching
var client = new ResilientGitHubClientFactory().Create(new ProductHeaderValue(agentName), credentials,new InMemoryCacheProvider());

// without caching
var client = new ResilientGitHubClientFactory().Create(new ProductHeaderValue(agentName), credentials);

```

# Logging

Integrating octokit.net.Extension in your source code is straightforward. In fact, instead of instantiating octokit's _GithubClient_ via constructor, you just need to use the _ResilientGitHubClientFactory_ which takes an optional _ILogger_ to log the events.

```C#

var logger = new LoggerFactory()
            .AddConsole().AddDebug()
            .CreateLogger("Github.Octokit.Logger");

var credentials = new Octokit.Credentials(token);

// with logging
var client = new ResilientGitHubClientFactory(logger).Create(new ProductHeaderValue(agentName), credentials);

// withiut logging
var client = new ResilientGitHubClientFactory().Create(new ProductHeaderValue(agentName), credentials);

```

# Policies

You are able to replace the built-in Polly policies with your own. In fact, _ResilientGitHubClientFactory.Create_ takes _params IAsyncPolicy[]_ as its last parameter. These policies define how we should act in case of happening any pre-defined catastrophic situation. If you don't pass any policies, _Octokit.Extention_ will use the [_DefaultResilientPolicies](https://github.com/mirsaeedi/octokit.net.extensions/blob/618e4e936c188c28d613e4b548924aa447635548/src/Octokit.Extensions/Resiliency/ResilientPolicies.cs#L65)_ as its policies.

```C#
// using a custom policy.
var policy = Policy.Handle<HttpRequestException>().RetryAsync(2);
var client = new ResilientGitHubClientFactory().Create(new ProductHeaderValue(agentName),policy);

// using all the built-in policies automatically all-together
var client = new ResilientGitHubClientFactory().Create(new ProductHeaderValue(agentName));

// using built-in policies selectively
var builtinPolicies= new ResilientPolicies();
var client = new ResilientGitHubClientFactory().Create(new ProductHeaderValue(agentName),builtinPolicies.DefaultRateLimitExceededExceptionPolicy,
builtinPolicies.DefaultAbuseExceptionExceptionPolicy);

```

# Install via [Nuget](https://www.nuget.org/packages/octokit.Extensions)

```powershell
Install-Package  Octokit.Extensions
```
