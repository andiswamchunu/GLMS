namespace GLMS.Api.Services;

/// <summary>The pure currency maths (no I/O) so it can be verified with unit tests.</summary>
public static class CurrencyConverter
{
    /// <summary>Converts an amount in a foreign currency to ZAR: amount x rate, rounded to 2 decimals (away from zero).</summary>
    /// <param name="amount">Amount in the foreign currency (must not be negative).</param>
    /// <param name="rateToZar">How many ZAR one unit of the foreign currency is worth (must be positive).</param>
    public static decimal ConvertToZar(decimal amount, decimal rateToZar)
    {
        if (amount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "The amount cannot be negative.");
        }

        if (rateToZar <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rateToZar), rateToZar, "The exchange rate must be greater than zero.");
        }

        return Math.Round(amount * rateToZar, 2, MidpointRounding.AwayFromZero);
    }
}
