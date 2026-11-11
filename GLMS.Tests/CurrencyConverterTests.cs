using GLMS.Web.Services;

namespace GLMS.Tests;

public class CurrencyConverterTests
{
    public static TheoryData<decimal, decimal, decimal> KnownConversions => new()
    {
        { 100m, 18.50m, 1850.00m },        // the simple case: $100 at R18.50
        { 1m, 1m, 1.00m },
        { 0m, 18.50m, 0.00m },
        { 1000m, 18.4567m, 18456.70m },
        { 250.75m, 17.90m, 4488.43m },     // 4488.425 rounds up (away from zero)
        { 0.01m, 18.50m, 0.19m },          // 0.185 rounds up
        { 99.99m, 18.2345m, 1823.27m },    // 1823.267655 rounds to 1823.27
    };

    [Theory]
    [MemberData(nameof(KnownConversions))]
    public void ConvertToZar_UsdAmountAtGivenRate_ReturnsCorrectRand(decimal usd, decimal rate, decimal expectedZar)
    {
        var result = CurrencyConverter.ConvertToZar(usd, rate);

        Assert.Equal(expectedZar, result);
    }

    [Fact]
    public void ConvertToZar_ResultIsRoundedToTwoDecimalPlaces()
    {
        var result = CurrencyConverter.ConvertToZar(33.33m, 18.3333m);

        Assert.Equal(Math.Round(result, 2), result);
        Assert.Equal(611.05m, result); // 33.33 x 18.3333 = 611.048889
    }

    [Fact]
    public void ConvertToZar_NegativeAmount_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CurrencyConverter.ConvertToZar(-1m, 18.5m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.5)]
    public void ConvertToZar_RateNotPositive_Throws(double rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CurrencyConverter.ConvertToZar(10m, (decimal)rate));
    }
}
