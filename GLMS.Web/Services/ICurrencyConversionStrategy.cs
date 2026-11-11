namespace GLMS.Web.Services;

/// <summary>
/// Strategy pattern (Part 1, section 3.2): interchangeable ways of obtaining an exchange rate.
/// Returns null when this strategy cannot supply a rate, so the next strategy can be tried.
/// </summary>
public interface ICurrencyConversionStrategy
{
    Task<ExchangeRateResult?> GetRateToZarAsync(string currencyCode, CancellationToken ct = default);
}
