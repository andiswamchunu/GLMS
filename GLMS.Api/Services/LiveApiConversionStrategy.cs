using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace GLMS.Api.Services;

/// <summary>Gets the current rate from the free ExchangeRate-API open endpoint (https://open.er-api.com) using HttpClient.</summary>
public sealed class LiveApiConversionStrategy : ICurrencyConversionStrategy
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly CurrencyOptions _options;
    private readonly TimeProvider _time;
    private readonly ILogger<LiveApiConversionStrategy> _logger;

    public LiveApiConversionStrategy(
        HttpClient http,
        IMemoryCache cache,
        IOptions<CurrencyOptions> options,
        TimeProvider time,
        ILogger<LiveApiConversionStrategy> logger)
    {
        _http = http;
        _cache = cache;
        _options = options.Value;
        _time = time;
        _logger = logger;
    }

    public static string FreshKey(string code) => $"fx:fresh:{code}";
    public static string LastGoodKey(string code) => $"fx:lastgood:{code}";

    public async Task<ExchangeRateResult?> GetRateToZarAsync(string currencyCode, CancellationToken ct = default)
    {
        var code = currencyCode.Trim().ToUpperInvariant();

        if (_cache.TryGetValue(FreshKey(code), out ExchangeRateResult? fresh) && fresh is not null)
        {
            return fresh;
        }

        try
        {
            using var response = await _http.GetAsync($"latest/{code}", ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var rate = ParseZarRate(json);

            var result = new ExchangeRateResult(code, rate, "Live exchange-rate API", _time.GetUtcNow().UtcDateTime, IsFallback: false);

            _cache.Set(FreshKey(code), result, TimeSpan.FromMinutes(_options.CacheMinutes));
            _cache.Set(LastGoodKey(code), result); // no expiry: used by CachedConversionStrategy when the API is down
            return result;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Live exchange-rate lookup for {Currency} failed; the next strategy will be used.", code);
            return null;
        }
    }

    /// <summary>Extracts the ZAR rate from an ExchangeRate-API response body.</summary>
    /// <exception cref="InvalidOperationException">The response is not a success or has no usable ZAR rate.</exception>
    /// <exception cref="JsonException">The body is not valid JSON.</exception>
    public static decimal ParseZarRate(string json)
    {
        var payload = JsonSerializer.Deserialize<ErApiResponse>(json, JsonOptions)
                      ?? throw new InvalidOperationException("The exchange-rate response was empty.");

        if (!string.Equals(payload.Result, "success", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The exchange-rate API did not report success.");
        }

        if (payload.Rates is null || !payload.Rates.TryGetValue("ZAR", out var zar) || zar <= 0)
        {
            throw new InvalidOperationException("The exchange-rate response did not contain a ZAR rate.");
        }

        return zar;
    }

    private sealed class ErApiResponse
    {
        [JsonPropertyName("result")]
        public string? Result { get; set; }

        [JsonPropertyName("rates")]
        public Dictionary<string, decimal>? Rates { get; set; }
    }
}
