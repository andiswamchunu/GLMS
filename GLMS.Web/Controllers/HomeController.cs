using System.Diagnostics;
using GLMS.Web.Data;
using GLMS.Shared.Models;
using GLMS.Web.Services;
using GLMS.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Web.Controllers;

public class HomeController : Controller
{
    private readonly GlmsDbContext _db;
    private readonly IContractService _contracts;
    private readonly TimeProvider _time;

    public HomeController(GlmsDbContext db, IContractService contracts, TimeProvider time)
    {
        _db = db;
        _contracts = contracts;
        _time = time;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        await _contracts.ExpireOverdueContractsAsync(ct);

        var today = _time.Today();
        var soon = today.AddDays(30);

        // Each query is independent, but they share one DbContext (which is not thread-safe), so they are awaited in turn.
        var byStatus = await _db.Contracts.GroupBy(c => c.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int CountOf(ContractStatus s) => byStatus.FirstOrDefault(x => x.Status == s)?.Count ?? 0;

        var model = new DashboardViewModel
        {
            ClientCount = await _db.Clients.CountAsync(ct),
            ActiveContracts = CountOf(ContractStatus.Active),
            OnHoldContracts = CountOf(ContractStatus.OnHold),
            ExpiredContracts = CountOf(ContractStatus.Expired),
            DraftContracts = CountOf(ContractStatus.Draft),
            OpenRequests = await _db.ServiceRequests.CountAsync(
                r => r.Status == ServiceRequestStatus.Pending || r.Status == ServiceRequestStatus.InProgress, ct),
            TotalCostZar = await _db.ServiceRequests
                .Where(r => r.Status != ServiceRequestStatus.Cancelled)
                .SumAsync(r => (decimal?)r.CostZar, ct) ?? 0m,
            ExpiringSoon = await _db.Contracts.AsNoTracking().Include(c => c.Client)
                .Where(c => c.Status == ContractStatus.Active && c.EndDate >= today && c.EndDate <= soon)
                .OrderBy(c => c.EndDate)
                .ToListAsync(ct)
        };

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
