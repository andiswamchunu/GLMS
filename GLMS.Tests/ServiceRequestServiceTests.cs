using GLMS.Tests.Helpers;
using GLMS.Web.Data;
using GLMS.Web.Models;
using GLMS.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Tests;

public class ServiceRequestServiceTests
{
    private const decimal Rate = 18.50m;

    private static (GlmsDbContext Db, ServiceRequestService Service, FakeCurrencyService Currency) Create(decimal rate = Rate)
    {
        var db = TestDb.Create();
        var currency = new FakeCurrencyService(rate);
        return (db, new ServiceRequestService(db, currency, FixedTimeProvider.Default()), currency);
    }

    [Fact]
    public async Task CreateAsync_ActiveContract_SavesRequestWithAutoCalculatedZarCost()
    {
        var (db, service, currency) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);

        var created = await service.CreateAsync(contract.Id, "Truck to Durban", 200m, "USD");

        var saved = await db.ServiceRequests.AsNoTracking().SingleAsync();
        Assert.Equal(created.Id, saved.Id);
        Assert.Equal(3700.00m, saved.CostZar);          // 200 USD x 18.50
        Assert.Equal(200m, saved.OriginalAmount);
        Assert.Equal("USD", saved.CurrencyCode);
        Assert.Equal(Rate, saved.ExchangeRate);
        Assert.Equal("Fake rate", saved.RateSource);
        Assert.Equal(ServiceRequestStatus.Pending, saved.Status);
        Assert.Equal(new DateTime(2026, 10, 15, 9, 30, 0, DateTimeKind.Utc), saved.CreatedAtUtc);
        Assert.Equal(1, currency.Calls);
    }

    [Fact]
    public async Task CreateAsync_TrimsDescriptionAndNormalisesCurrencyCode()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);

        var created = await service.CreateAsync(contract.Id, "  Sea freight  ", 10m, "eur");

        Assert.Equal("Sea freight", created.Description);
        Assert.Equal("EUR", created.CurrencyCode);
    }

    [Theory]
    [InlineData(ContractStatus.Expired)]
    [InlineData(ContractStatus.OnHold)]
    [InlineData(ContractStatus.Draft)]
    public async Task CreateAsync_ContractNotActive_IsRejectedAndNothingIsSaved(ContractStatus status)
    {
        var (db, service, currency) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, status);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            service.CreateAsync(contract.Id, "Should not be created", 100m, "USD"));

        Assert.Contains(status == ContractStatus.OnHold ? "On Hold" : status.ToString(), ex.Message);
        Assert.Empty(db.ServiceRequests);
        Assert.Equal(0, currency.Calls); // the rate API is not even called for a blocked request
    }

    [Fact]
    public async Task CreateAsync_ExpiredContract_ErrorMessageSaysExpired()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Expired);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(contract.Id, "x", 1m, "USD"));

        Assert.Contains("Expired", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_OnHoldContract_ErrorMessageSaysOnHold()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.OnHold);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(contract.Id, "x", 1m, "USD"));

        Assert.Contains("On Hold", ex.Message);
    }

    [Fact]
    public async Task CreateAsync_ActiveContractPastItsEndDate_IsRejected()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        // Stored as Active, but the end date (2026-10-14) is before the fixed "today" (2026-10-15).
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active, new DateTime(2025, 1, 1), new DateTime(2026, 10, 14));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(contract.Id, "Late request", 5m, "USD"));

        Assert.Empty(db.ServiceRequests);
    }

    [Fact]
    public async Task CreateAsync_UnknownContract_Throws()
    {
        var (db, service, _) = Create();
        await using var _db = db;

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(999, "x", 1m, "USD"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task CreateAsync_AmountNotPositive_Throws(double amount)
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(contract.Id, "x", (decimal)amount, "USD"));

        Assert.Empty(db.ServiceRequests);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_BlankDescription_Throws(string description)
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.CreateAsync(contract.Id, description, 10m, "USD"));
    }

    [Fact]
    public async Task CreateAsync_RateServiceUnavailable_PropagatesAndSavesNothing()
    {
        var db = TestDb.Create();
        await using var _db = db;
        var failing = new CurrencyService(new[] { new FakeStrategy(null) }, FixedTimeProvider.Default());
        var service = new ServiceRequestService(db, failing, FixedTimeProvider.Default());
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);

        await Assert.ThrowsAsync<CurrencyUnavailableException>(() => service.CreateAsync(contract.Id, "x", 10m, "USD"));

        Assert.Empty(db.ServiceRequests);
    }

    [Fact]
    public async Task ListAsync_FiltersByContract_NewestFirst()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var a = await TestDb.AddContractAsync(db, client, ContractStatus.Active);
        var b = await TestDb.AddContractAsync(db, client, ContractStatus.Active);
        await service.CreateAsync(a.Id, "A1", 1m, "USD");
        await service.CreateAsync(b.Id, "B1", 1m, "USD");
        await service.CreateAsync(a.Id, "A2", 1m, "USD");

        var forA = await service.ListAsync(a.Id);
        var all = await service.ListAsync();

        Assert.Equal(new[] { "A2", "A1" }, forA.Select(r => r.Description).ToArray());
        Assert.Equal(3, all.Count);
    }

    [Fact]
    public async Task ChangeStatusAsync_FollowsTheWorkflow()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);
        var request = await service.CreateAsync(contract.Id, "x", 1m, "USD");

        await service.ChangeStatusAsync(request.Id, ServiceRequestStatus.InProgress);
        await service.ChangeStatusAsync(request.Id, ServiceRequestStatus.Completed);

        Assert.Equal(ServiceRequestStatus.Completed, (await db.ServiceRequests.AsNoTracking().SingleAsync()).Status);
        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ChangeStatusAsync(request.Id, ServiceRequestStatus.Pending));
    }

    [Fact]
    public async Task ChangeStatusAsync_SkippingAStep_IsRejected()
    {
        var (db, service, _) = Create();
        await using var _db = db;
        var client = await TestDb.AddClientAsync(db);
        var contract = await TestDb.AddContractAsync(db, client, ContractStatus.Active);
        var request = await service.CreateAsync(contract.Id, "x", 1m, "USD");

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.ChangeStatusAsync(request.Id, ServiceRequestStatus.Completed));
    }
}
