using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GLMS.Web.Services;

/// <summary>
/// Used when the external API is unavailable: the last successfully retrieved rate, otherwise the configured fallback rate.
/// </summary>
public sealed class CachedConversionStrategy : ICurrencyConversionStrategy
{
    private readonly IMemoryCache _cache;
    private readonly CurrencyOptions _options;
    private readonly TimeProvider _time;

    public CachedConversionStrategy(IMemoryCache cache, IOptions<CurrencyOptions> options, TimeProvider time)
    {
        _cache = cache;
        _options = options.Value;
        _time = time;
    }

    public Task<ExchangeRateResult?> GetRateToZarAsync(string currencyCode, CancellationToken ct = default)
    {
        var code = currencyCode.Trim().ToUpperInvariant();

        if (_cache.TryGetValue(LiveApiConversionStrategy.LastGoodKey(code), out ExchangeRateResult? last) && last is not null)
        {
            return Task.FromResult<ExchangeRateResult?>(last with { Source = "Last known rate (API unavailable)", IsFallback = true });
        }

        if (_options.FallbackRates.TryGetValue(code, out var configured) && configured > 0)
        {
            return Task.FromResult<ExchangeRateResult?>(
                new ExchangeRateResult(code, configured, "Configured fallback rate (API unavailable)", _time.GetUtcNow().UtcDateTime, IsFallback: true));
        }

        return Task.FromResult<ExchangeRateResult?>(null);
    }
}
