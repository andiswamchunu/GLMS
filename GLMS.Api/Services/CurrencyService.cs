using System.Text.RegularExpressions;


namespace GLMS.Api.Services;

public interface ICurrencyService
{
    /// <summary>Gets how many ZAR one unit of <paramref name="currencyCode"/> is worth right now.</summary>
    /// <exception cref="CurrencyUnavailableException">No strategy could supply a rate.</exception>
    /// <exception cref="BusinessRuleException">The currency code is not a valid 3-letter ISO code.</exception>
    Task<ExchangeRateResult> GetRateToZarAsync(string currencyCode, CancellationToken ct = default);
}

/// <summary>Context of the Strategy pattern: tries each registered strategy in order (live API first, then cached/fallback).</summary>
public sealed partial class CurrencyService : ICurrencyService
{
    private readonly IEnumerable<ICurrencyConversionStrategy> _strategies;
    private readonly TimeProvider _time;

    public CurrencyService(IEnumerable<ICurrencyConversionStrategy> strategies, TimeProvider time)
    {
        _strategies = strategies;
        _time = time;
    }

    [GeneratedRegex("^[A-Za-z]{3}$")]
    private static partial Regex CodeRegex();

    public async Task<ExchangeRateResult> GetRateToZarAsync(string currencyCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(currencyCode) || !CodeRegex().IsMatch(currencyCode.Trim()))
        {
            throw new BusinessRuleException("The currency must be a 3-letter code such as USD or EUR.");
        }

        var code = currencyCode.Trim().ToUpperInvariant();

        if (code == "ZAR")
        {
            return new ExchangeRateResult("ZAR", 1m, "No conversion needed", _time.GetUtcNow().UtcDateTime, IsFallback: false);
        }

        foreach (var strategy in _strategies)
        {
            var result = await strategy.GetRateToZarAsync(code, ct);
            if (result is not null)
            {
                return result;
            }
        }

        throw new CurrencyUnavailableException(
            $"The {code} to ZAR exchange rate is currently unavailable. Please try again shortly.");
    }
}
