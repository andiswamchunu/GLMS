using GLMS.Tests.Helpers;
using GLMS.Web.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GLMS.Tests;

public class CurrencyServiceTests
{
    private const string SuccessJson =
        "{\"result\":\"success\",\"base_code\":\"USD\",\"rates\":{\"USD\":1,\"ZAR\":18.25,\"EUR\":0.92}}";

    private static readonly DateTime Now = new(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc);

    private static ExchangeRateResult Rate(decimal value, string source = "test") =>
        new("USD", value, source, Now, IsFallback: false);

    private static LiveApiConversionStrategy CreateLive(StubHttpMessageHandler handler, IMemoryCache? cache = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://rates.test/v6/") };
        return new LiveApiConversionStrategy(
            http,
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            Options.Create(new CurrencyOptions { CacheMinutes = 60 }),
            FixedTimeProvider.Default(),
            NullLogger<LiveApiConversionStrategy>.Instance);
    }

    // ---------- live API strategy (HttpClient) ----------

    [Fact]
    public async Task LiveApi_SuccessfulResponse_ReturnsTheZarRate()
    {
        var handler = StubHttpMessageHandler.Json(SuccessJson);

        var result = await CreateLive(handler).GetRateToZarAsync("USD");

        Assert.NotNull(result);
        Assert.Equal(18.25m, result!.RateToZar);
        Assert.Equal("USD", result.Currency);
        Assert.False(result.IsFallback);
        Assert.Contains("Live", result.Source);
    }

    [Fact]
    public async Task LiveApi_CallsTheLatestEndpointForTheRequestedCurrency()
    {
        var handler = StubHttpMessageHandler.Json(SuccessJson);

        await CreateLive(handler).GetRateToZarAsync("usd");

        Assert.Equal("/v6/latest/USD", handler.LastRequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task LiveApi_SecondRequestWithinCacheWindow_DoesNotCallTheApiAgain()
    {
        var handler = StubHttpMessageHandler.Json(SuccessJson);
        var strategy = CreateLive(handler);

        await strategy.GetRateToZarAsync("USD");
        var second = await strategy.GetRateToZarAsync("USD");

        Assert.Equal(1, handler.Calls);
        Assert.Equal(18.25m, second!.RateToZar);
    }

    [Fact]
    public async Task LiveApi_ServerError_ReturnsNullSoTheNextStrategyCanBeUsed()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError));

        Assert.Null(await CreateLive(handler).GetRateToZarAsync("USD"));
    }

    [Fact]
    public async Task LiveApi_NetworkFailure_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => throw new HttpRequestException("DNS failure"));

        Assert.Null(await CreateLive(handler).GetRateToZarAsync("USD"));
    }

    [Theory]
    [InlineData("{\"result\":\"error\",\"error-type\":\"quota-reached\"}")]
    [InlineData("{\"result\":\"success\",\"rates\":{\"EUR\":0.9}}")]          // no ZAR in the response
    [InlineData("{\"result\":\"success\",\"rates\":{\"ZAR\":0}}")]            // nonsense rate
    [InlineData("this is not json")]
    public async Task LiveApi_UnusableResponseBody_ReturnsNull(string body)
    {
        var handler = StubHttpMessageHandler.Json(body);

        Assert.Null(await CreateLive(handler).GetRateToZarAsync("USD"));
    }

    [Fact]
    public async Task LiveApi_CallerCancellation_IsNotSwallowed()
    {
        var handler = StubHttpMessageHandler.Json(SuccessJson);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateLive(handler).GetRateToZarAsync("USD", cts.Token));
    }

    [Fact]
    public void ParseZarRate_ValidJson_ReturnsRate()
    {
        Assert.Equal(18.25m, LiveApiConversionStrategy.ParseZarRate(SuccessJson));
    }

    [Fact]
    public void ParseZarRate_MissingZar_ThrowsInvalidOperation()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LiveApiConversionStrategy.ParseZarRate("{\"result\":\"success\",\"rates\":{\"EUR\":0.9}}"));
    }

    // ---------- cached / fallback strategy ----------

    [Fact]
    public async Task Cached_AfterASuccessfulLiveLookup_ReturnsTheLastKnownRateMarkedAsFallback()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        await CreateLive(StubHttpMessageHandler.Json(SuccessJson), cache).GetRateToZarAsync("USD");
        var cached = new CachedConversionStrategy(cache, Options.Create(new CurrencyOptions()), FixedTimeProvider.Default());

        var result = await cached.GetRateToZarAsync("USD");

        Assert.Equal(18.25m, result!.RateToZar);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task Cached_NothingCached_UsesConfiguredFallbackRate()
    {
        var options = new CurrencyOptions { FallbackRates = new() { ["USD"] = 17.75m } };
        var cached = new CachedConversionStrategy(new MemoryCache(new MemoryCacheOptions()), Options.Create(options), FixedTimeProvider.Default());

        var result = await cached.GetRateToZarAsync("usd");

        Assert.Equal(17.75m, result!.RateToZar);
        Assert.True(result.IsFallback);
        Assert.Contains("fallback", result.Source);
    }

    [Fact]
    public async Task Cached_NothingCachedAndNoFallbackForThatCurrency_ReturnsNull()
    {
        var cached = new CachedConversionStrategy(new MemoryCache(new MemoryCacheOptions()), Options.Create(new CurrencyOptions()), FixedTimeProvider.Default());

        Assert.Null(await cached.GetRateToZarAsync("JPY"));
    }

    // ---------- CurrencyService (the Strategy context) ----------

    [Fact]
    public async Task Service_UsesTheFirstStrategyThatReturnsARate()
    {
        var first = new FakeStrategy(Rate(18m, "first"));
        var second = new FakeStrategy(Rate(99m, "second"));
        var service = new CurrencyService(new[] { first, second }, FixedTimeProvider.Default());

        var result = await service.GetRateToZarAsync("USD");

        Assert.Equal(18m, result.RateToZar);
        Assert.Equal(1, first.Calls);
        Assert.Equal(0, second.Calls);
    }

    [Fact]
    public async Task Service_FirstStrategyFails_FallsBackToTheNext()
    {
        var first = new FakeStrategy(null);
        var second = new FakeStrategy(Rate(17m, "second"));
        var service = new CurrencyService(new[] { first, second }, FixedTimeProvider.Default());

        var result = await service.GetRateToZarAsync("USD");

        Assert.Equal(17m, result.RateToZar);
        Assert.Equal(1, first.Calls);
        Assert.Equal(1, second.Calls);
    }

    [Fact]
    public async Task Service_AllStrategiesFail_ThrowsCurrencyUnavailable()
    {
        var service = new CurrencyService(new[] { new FakeStrategy(null), new FakeStrategy(null) }, FixedTimeProvider.Default());

        var ex = await Assert.ThrowsAsync<CurrencyUnavailableException>(() => service.GetRateToZarAsync("USD"));

        Assert.Contains("USD", ex.Message);
    }

    [Fact]
    public async Task Service_LiveApiDown_EndToEndFallsBackToConfiguredRate()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var down = new StubHttpMessageHandler(_ => throw new HttpRequestException("offline"));
        var options = Options.Create(new CurrencyOptions { FallbackRates = new() { ["USD"] = 18.5m } });
        var service = new CurrencyService(
            new ICurrencyConversionStrategy[]
            {
                CreateLive(down, cache),
                new CachedConversionStrategy(cache, options, FixedTimeProvider.Default())
            },
            FixedTimeProvider.Default());

        var result = await service.GetRateToZarAsync("USD");

        Assert.Equal(18.5m, result.RateToZar);
        Assert.True(result.IsFallback);
    }

    [Fact]
    public async Task Service_NormalisesCurrencyCodeBeforeCallingStrategies()
    {
        var strategy = new FakeStrategy(Rate(18m));
        var service = new CurrencyService(new[] { strategy }, FixedTimeProvider.Default());

        await service.GetRateToZarAsync("  usd ");

        Assert.Equal("USD", strategy.LastCode);
    }

    [Fact]
    public async Task Service_Zar_NeedsNoConversion()
    {
        var strategy = new FakeStrategy(Rate(18m));
        var service = new CurrencyService(new[] { strategy }, FixedTimeProvider.Default());

        var result = await service.GetRateToZarAsync("zar");

        Assert.Equal(1m, result.RateToZar);
        Assert.Equal(0, strategy.Calls);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("US")]
    [InlineData("USDX")]
    [InlineData("12$")]
    public async Task Service_InvalidCurrencyCode_ThrowsBusinessRuleException(string code)
    {
        var service = new CurrencyService(new[] { new FakeStrategy(Rate(18m)) }, FixedTimeProvider.Default());

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetRateToZarAsync(code));
    }
}
