namespace GLMS.Web.Services;

public class CurrencyOptions
{
    public string BaseUrl { get; set; } = "https://open.er-api.com/v6/";
    public int TimeoutSeconds { get; set; } = 5;
    public int CacheMinutes { get; set; } = 60;

    /// <summary>Offline safety net: currency code -> ZAR. Used only if the API and the cache both have nothing.</summary>
    public Dictionary<string, decimal> FallbackRates { get; set; } = new();
}

/// <param name="Currency">ISO code of the foreign currency (e.g. USD).</param>
/// <param name="RateToZar">1 unit of <paramref name="Currency"/> = this many ZAR.</param>
/// <param name="Source">Where the rate came from (shown in the UI).</param>
/// <param name="RetrievedAtUtc">When the rate was obtained.</param>
/// <param name="IsFallback">True when the live API could not be reached.</param>
public sealed record ExchangeRateResult(string Currency, decimal RateToZar, string Source, DateTime RetrievedAtUtc, bool IsFallback);
