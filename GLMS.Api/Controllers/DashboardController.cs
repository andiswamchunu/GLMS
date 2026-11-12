using GLMS.Api.Data;
using GLMS.Api.Services;
using GLMS.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace GLMS.Api.Controllers;
[ApiController]
[Route("api/[controller]")]
public class DashboardController : ControllerBase
{
    private readonly GlmsDbContext _db;
    private readonly IContractService _contracts;
    private readonly TimeProvider _time;
    public DashboardController(GlmsDbContext db, IContractService contracts, TimeProvider time) { _db = db; _contracts = contracts; _time = time; }
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        await _contracts.ExpireOverdueContractsAsync(ct);
        var today = _time.GetUtcNow().UtcDateTime.Date;
        var counts = await _db.Contracts.AsNoTracking().GroupBy(x => x.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToListAsync(ct);
        int Count(ContractStatus s) => counts.FirstOrDefault(x => x.Status == s)?.Count ?? 0;
        var soon = await _db.Contracts.AsNoTracking().Include(x => x.Client).Where(x => x.Status == ContractStatus.Active && x.EndDate >= today && x.EndDate <= today.AddDays(30)).OrderBy(x => x.EndDate).ToListAsync(ct);
        return Ok(new { ClientCount = await _db.Clients.CountAsync(ct), ActiveContracts = Count(ContractStatus.Active), OnHoldContracts = Count(ContractStatus.OnHold), ExpiredContracts = Count(ContractStatus.Expired), DraftContracts = Count(ContractStatus.Draft), OpenRequests = await _db.ServiceRequests.CountAsync(x => x.Status == ServiceRequestStatus.Pending || x.Status == ServiceRequestStatus.InProgress, ct), TotalCostZar = await _db.ServiceRequests.Where(x => x.Status != ServiceRequestStatus.Cancelled).SumAsync(x => (decimal?)x.CostZar, ct) ?? 0m, ExpiringSoon = soon.Select(x => new { x.Id, x.ClientId, x.StartDate, x.EndDate, x.Status, x.ServiceLevel, Client = x.Client is null ? null : new { x.Client.Id, x.Client.Name } }) });
    }
}
