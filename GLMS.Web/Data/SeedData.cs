using GLMS.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Web.Data;

/// <summary>Demo data so the screens are not empty on first run.</summary>
public static class SeedData
{
    public static async Task InitialiseAsync(GlmsDbContext db, DateTime today)
    {
        if (await db.Clients.AnyAsync())
        {
            return;
        }

        var clients = new[]
        {
            new Client { Name = "Cape Fresh Exporters", ContactDetails = "ops@capefresh.example | +27 21 555 0101", Region = "Western Cape" },
            new Client { Name = "Rhine Auto Parts GmbH", ContactDetails = "logistik@rhineauto.example | +49 221 555 0142", Region = "Europe" },
            new Client { Name = "Gulf Star Trading", ContactDetails = "shipping@gulfstar.example | +971 4 555 0177", Region = "Middle East" }
        };
        db.Clients.AddRange(clients);
        await db.SaveChangesAsync();

        var contracts = new[]
        {
            new Contract { ClientId = clients[0].Id, StartDate = today.AddMonths(-3), EndDate = today.AddMonths(9), Status = ContractStatus.Active, ServiceLevel = ServiceLevel.Premium },
            new Contract { ClientId = clients[1].Id, StartDate = today.AddMonths(-1), EndDate = today.AddMonths(11), Status = ContractStatus.Active, ServiceLevel = ServiceLevel.Standard },
            new Contract { ClientId = clients[2].Id, StartDate = today.AddMonths(-8), EndDate = today.AddMonths(4), Status = ContractStatus.OnHold, ServiceLevel = ServiceLevel.Express },
            new Contract { ClientId = clients[0].Id, StartDate = today.AddYears(-2), EndDate = today.AddMonths(-2), Status = ContractStatus.Expired, ServiceLevel = ServiceLevel.Standard },
            new Contract { ClientId = clients[1].Id, StartDate = today.AddDays(14), EndDate = today.AddYears(1), Status = ContractStatus.Draft, ServiceLevel = ServiceLevel.Premium }
        };
        db.Contracts.AddRange(contracts);
        await db.SaveChangesAsync();

        db.ServiceRequests.AddRange(
            new ServiceRequest
            {
                ContractId = contracts[0].Id, Description = "Reefer truck Cape Town to Johannesburg",
                OriginalAmount = 1200m, CurrencyCode = "USD", ExchangeRate = 18.50m, RateSource = "Seed data",
                CostZar = 22200m, Status = ServiceRequestStatus.InProgress, CreatedAtUtc = DateTime.UtcNow.AddDays(-5)
            },
            new ServiceRequest
            {
                ContractId = contracts[1].Id, Description = "Sea freight Durban to Hamburg (1 x 40ft container)",
                OriginalAmount = 3400m, CurrencyCode = "EUR", ExchangeRate = 20.10m, RateSource = "Seed data",
                CostZar = 68340m, Status = ServiceRequestStatus.Pending, CreatedAtUtc = DateTime.UtcNow.AddDays(-1)
            });
        await db.SaveChangesAsync();
    }
}
