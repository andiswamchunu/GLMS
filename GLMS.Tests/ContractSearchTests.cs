using GLMS.Tests.Helpers;
using GLMS.Shared.Models;
using GLMS.Web.Services;

namespace GLMS.Tests;

/// <summary>Tests for the LINQ search / filter used by the Admin contracts screen.</summary>
public class ContractSearchTests
{
    private static Contract C(int id, string client, ContractStatus status, string start, string end) => new()
    {
        Id = id,
        ClientId = client == "Alpha" ? 1 : 2,
        Client = new Client { Id = client == "Alpha" ? 1 : 2, Name = client + " Freight", ContactDetails = "x", Region = "x" },
        Status = status,
        StartDate = DateTime.Parse(start),
        EndDate = DateTime.Parse(end)
    };

    private static readonly List<Contract> Data = new()
    {
        C(1, "Alpha", ContractStatus.Active,  "2026-01-01", "2026-06-30"),
        C(2, "Alpha", ContractStatus.Expired, "2025-01-01", "2025-12-31"),
        C(3, "Beta",  ContractStatus.Active,  "2026-03-01", "2027-02-28"),
        C(4, "Beta",  ContractStatus.OnHold,  "2026-05-01", "2026-12-31"),
        C(5, "Beta",  ContractStatus.Draft,   "2027-01-01", "2027-12-31"),
    };

    private static int[] Ids(ContractSearchCriteria criteria) =>
        Data.AsQueryable().ApplyFilter(criteria).Select(c => c.Id).ToArray();

    [Fact]
    public void ApplyFilter_NoCriteria_ReturnsEverythingOrderedByEndDate()
    {
        Assert.Equal(new[] { 2, 1, 4, 3, 5 }, Ids(new ContractSearchCriteria()));
    }

    [Theory]
    [InlineData(ContractStatus.Active, new[] { 1, 3 })]
    [InlineData(ContractStatus.Expired, new[] { 2 })]
    [InlineData(ContractStatus.OnHold, new[] { 4 })]
    [InlineData(ContractStatus.Draft, new[] { 5 })]
    public void ApplyFilter_ByStatus_ReturnsOnlyThatStatus(ContractStatus status, int[] expected)
    {
        Assert.Equal(expected, Ids(new ContractSearchCriteria { Status = status }));
    }

    [Fact]
    public void ApplyFilter_ByDateRange_ReturnsContractsRunningInThatWindow()
    {
        // Window 1 Jul 2026 - 31 Aug 2026: contract 1 ended 30 Jun (out), 2 long ended (out), 3 and 4 running (in), 5 starts 2027 (out)
        var ids = Ids(new ContractSearchCriteria { From = new DateTime(2026, 7, 1), To = new DateTime(2026, 8, 31) });

        Assert.Equal(new[] { 4, 3 }, ids);
    }

    [Fact]
    public void ApplyFilter_DateRange_IncludesContractsTouchingTheBoundaryDays()
    {
        // Contract 1 ends exactly on 30 Jun 2026 and contract 5 starts exactly on 1 Jan 2027.
        Assert.Contains(1, Ids(new ContractSearchCriteria { From = new DateTime(2026, 6, 30) }));
        Assert.Contains(5, Ids(new ContractSearchCriteria { To = new DateTime(2027, 1, 1) }));
        Assert.DoesNotContain(1, Ids(new ContractSearchCriteria { From = new DateTime(2026, 7, 1) }));
        Assert.DoesNotContain(5, Ids(new ContractSearchCriteria { To = new DateTime(2026, 12, 31) }));
    }

    [Fact]
    public void ApplyFilter_OnlyFromDate_ReturnsContractsStillRunningOnOrAfterIt()
    {
        Assert.Equal(new[] { 4, 3, 5 }, Ids(new ContractSearchCriteria { From = new DateTime(2026, 7, 1) }));
    }

    [Fact]
    public void ApplyFilter_OnlyToDate_ReturnsContractsStartedOnOrBeforeIt()
    {
        Assert.Equal(new[] { 2, 1 }, Ids(new ContractSearchCriteria { To = new DateTime(2026, 2, 1) }));
    }

    [Fact]
    public void ApplyFilter_DateRangeAndStatusTogether_AppliesBoth()
    {
        var ids = Ids(new ContractSearchCriteria
        {
            From = new DateTime(2026, 7, 1),
            To = new DateTime(2026, 8, 31),
            Status = ContractStatus.Active
        });

        Assert.Equal(new[] { 3 }, ids);
    }

    [Fact]
    public void ApplyFilter_TimeOfDayInDates_IsIgnored()
    {
        var ids = Ids(new ContractSearchCriteria { From = new DateTime(2026, 6, 30, 23, 59, 0) });

        Assert.Contains(1, ids);
    }

    [Fact]
    public void ApplyFilter_ByClientIdAndName_FiltersOnClient()
    {
        Assert.Equal(new[] { 2, 1 }, Ids(new ContractSearchCriteria { ClientId = 1 }));
        Assert.Equal(new[] { 4, 3, 5 }, Ids(new ContractSearchCriteria { Query = "Beta" }));
        Assert.Empty(Ids(new ContractSearchCriteria { Query = "Nobody" }));
    }

    [Fact]
    public void ApplyFilter_NoMatches_ReturnsEmpty()
    {
        Assert.Empty(Ids(new ContractSearchCriteria { Status = ContractStatus.Draft, From = new DateTime(2026, 1, 1), To = new DateTime(2026, 2, 1) }));
    }

    // ----- the same filter run through EF Core (database provider) and the service -----

    [Fact]
    public async Task SearchAsync_AgainstDatabase_FiltersByDateRangeAndStatus()
    {
        await using var db = TestDb.Create();
        var client = await TestDb.AddClientAsync(db);
        var inRange = await TestDb.AddContractAsync(db, client, ContractStatus.Active, new DateTime(2026, 9, 1), new DateTime(2027, 9, 1));
        await TestDb.AddContractAsync(db, client, ContractStatus.OnHold, new DateTime(2026, 9, 1), new DateTime(2027, 9, 1));
        await TestDb.AddContractAsync(db, client, ContractStatus.Active, new DateTime(2028, 1, 1), new DateTime(2029, 1, 1));
        var service = new ContractService(db, new InMemoryAgreementStorage(), FixedTimeProvider.Default());

        var results = await service.SearchAsync(new ContractSearchCriteria
        {
            From = new DateTime(2026, 10, 1),
            To = new DateTime(2026, 12, 31),
            Status = ContractStatus.Active
        });

        var single = Assert.Single(results);
        Assert.Equal(inRange.Id, single.Id);
        Assert.NotNull(single.Client); // client is loaded for display
    }

    [Fact]
    public async Task SearchAsync_MarksOverdueContractsExpiredFirst_SoTheExpiredFilterIsAccurate()
    {
        await using var db = TestDb.Create();
        var client = await TestDb.AddClientAsync(db);
        // "Active" in the database but ended yesterday (today is 2026-10-15 in the fixed clock).
        var overdue = await TestDb.AddContractAsync(db, client, ContractStatus.Active, new DateTime(2025, 1, 1), new DateTime(2026, 10, 14));
        var service = new ContractService(db, new InMemoryAgreementStorage(), FixedTimeProvider.Default());

        var expired = await service.SearchAsync(new ContractSearchCriteria { Status = ContractStatus.Expired });

        Assert.Equal(overdue.Id, Assert.Single(expired).Id);
        Assert.Empty(await service.SearchAsync(new ContractSearchCriteria { Status = ContractStatus.Active }));
    }
}
