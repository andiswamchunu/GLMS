using System.Text;
using GLMS.Web.Data;
using GLMS.Shared.Models;
using GLMS.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Tests.Helpers;

/// <summary>A clock frozen at a known moment so date rules are deterministic.</summary>
public sealed class FixedTimeProvider : TimeProvider
{
    private readonly DateTimeOffset _now;

    public FixedTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc));

    public override DateTimeOffset GetUtcNow() => _now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

    /// <summary>The "today" used across the tests: 15 October 2026.</summary>
    public static FixedTimeProvider Default() => new(new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc));
}

public static class TestDb
{
    /// <summary>A fresh, isolated in-memory database for each test.</summary>
    public static GlmsDbContext Create()
    {
        var options = new DbContextOptionsBuilder<GlmsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new GlmsDbContext(options);
    }

    public static async Task<Client> AddClientAsync(GlmsDbContext db, string name = "Test Client", string region = "Gauteng")
    {
        var client = new Client { Name = name, ContactDetails = "test@example.com", Region = region };
        db.Clients.Add(client);
        await db.SaveChangesAsync();
        return client;
    }

    public static async Task<Contract> AddContractAsync(
        GlmsDbContext db,
        Client client,
        ContractStatus status,
        DateTime? start = null,
        DateTime? end = null,
        ServiceLevel level = ServiceLevel.Standard)
    {
        var contract = new Contract
        {
            ClientId = client.Id,
            Status = status,
            StartDate = (start ?? new DateTime(2026, 1, 1)).Date,
            EndDate = (end ?? new DateTime(2027, 1, 1)).Date,
            ServiceLevel = level
        };
        db.Contracts.Add(contract);
        await db.SaveChangesAsync();
        return contract;
    }
}

public static class PdfSamples
{
    public static byte[] ValidPdf() => Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\ntrailer\n<<>>\n%%EOF");
    public static MemoryStream ValidPdfStream() => new(ValidPdf());

    /// <summary>The first bytes of a Windows executable ("MZ").</summary>
    public static byte[] ExecutableBytes() => new byte[] { 0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00 };
}

/// <summary>Keeps "stored" agreements in memory instead of on disk.</summary>
public sealed class InMemoryAgreementStorage : IAgreementStorage
{
    public Dictionary<string, byte[]> Files { get; } = new();

    public async Task<string> SaveAsync(Stream content, CancellationToken ct = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var name = $"{Guid.NewGuid():N}.pdf";
        Files[name] = buffer.ToArray();
        return name;
    }

    public Stream? OpenRead(string storedName) =>
        Files.TryGetValue(storedName, out var bytes) ? new MemoryStream(bytes) : null;

    public bool Delete(string storedName) => Files.Remove(storedName);
}

public sealed class FakeCurrencyService : ICurrencyService
{
    private readonly decimal _rate;
    public int Calls { get; private set; }
    public string? LastCurrency { get; private set; }

    public FakeCurrencyService(decimal rate) => _rate = rate;

    public Task<ExchangeRateResult> GetRateToZarAsync(string currencyCode, CancellationToken ct = default)
    {
        Calls++;
        LastCurrency = currencyCode;
        return Task.FromResult(new ExchangeRateResult(
            currencyCode.ToUpperInvariant(), _rate, "Fake rate", new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc), IsFallback: false));
    }
}

public sealed class FakeStrategy : ICurrencyConversionStrategy
{
    private readonly ExchangeRateResult? _result;
    public int Calls { get; private set; }
    public string? LastCode { get; private set; }

    public FakeStrategy(ExchangeRateResult? result) => _result = result;

    public Task<ExchangeRateResult?> GetRateToZarAsync(string currencyCode, CancellationToken ct = default)
    {
        Calls++;
        LastCode = currencyCode;
        return Task.FromResult(_result);
    }
}

/// <summary>Lets a test decide what the "external API" answers, and counts the calls.</summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
    public int Calls { get; private set; }
    public Uri? LastRequestUri { get; private set; }

    public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

    public static StubHttpMessageHandler Json(string json) =>
        new(_ => new HttpResponseMessage(System.Net.HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Calls++;
        LastRequestUri = request.RequestUri;
        return Task.FromResult(_responder(request));
    }
}
