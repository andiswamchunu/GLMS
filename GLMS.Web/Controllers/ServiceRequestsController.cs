using GLMS.Web.Data;
using GLMS.Shared.Models;
using GLMS.Web.Services;
using GLMS.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Web.Controllers;

public class ServiceRequestsController : Controller
{
    private readonly GlmsDbContext _db;
    private readonly IServiceRequestService _requests;
    private readonly ICurrencyService _currency;
    private readonly TimeProvider _time;

    public ServiceRequestsController(
        GlmsDbContext db, IServiceRequestService requests, ICurrencyService currency, TimeProvider time)
    {
        _db = db;
        _requests = requests;
        _currency = currency;
        _time = time;
    }

    public async Task<IActionResult> Index(CancellationToken ct) => View(await _requests.ListAsync(ct: ct));

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var request = await _requests.GetAsync(id, ct);
        return request is null ? NotFound() : View(request);
    }

    public async Task<IActionResult> Create(int? contractId, CancellationToken ct)
    {
        var model = new ServiceRequestCreateViewModel
        {
            ContractId = contractId,
            Contracts = await ContractOptionsAsync(ct)
        };
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ServiceRequestCreateViewModel model, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var created = await _requests.CreateAsync(
                    model.ContractId!.Value, model.Description, model.Amount!.Value, model.CurrencyCode, ct);

                TempData["Success"] = $"Service request created. Cost saved as R {created.CostZar:N2}.";
                return RedirectToAction(nameof(Details), new { id = created.Id });
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        model.Contracts = await ContractOptionsAsync(ct);
        return View(model);
    }

    /// <summary>JSON endpoint used by the Create page to show the live rate and auto-calculate the ZAR cost as the user types.</summary>
    [HttpGet]
    public async Task<IActionResult> Rate(string currency = "USD", CancellationToken ct = default)
    {
        try
        {
            var rate = await _currency.GetRateToZarAsync(currency, ct);
            return Json(new
            {
                currency = rate.Currency,
                rate = rate.RateToZar,
                source = rate.Source,
                isFallback = rate.IsFallback,
                retrievedAtUtc = rate.RetrievedAtUtc
            });
        }
        catch (CurrencyUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, ServiceRequestStatus status, CancellationToken ct)
    {
        try
        {
            await _requests.ChangeStatusAsync(id, status, ct);
            TempData["Success"] = $"The request is now {status}.";
        }
        catch (BusinessRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task<IReadOnlyList<ContractOption>> ContractOptionsAsync(CancellationToken ct)
    {
        var today = _time.Today();
        var contracts = await _db.Contracts.AsNoTracking().Include(c => c.Client)
            .OrderBy(c => c.Client!.Name).ThenBy(c => c.EndDate)
            .ToListAsync(ct);

        return contracts.Select(c =>
        {
            var rule = ContractRules.CanCreateServiceRequest(c, today);
            return new ContractOption
            {
                Id = c.Id,
                Label = $"#{c.Id} · {c.Client?.Name} · {c.ServiceLevel} · {c.Status} · ends {c.EndDate:yyyy-MM-dd}",
                Eligible = rule.Allowed,
                Reason = rule.Reason
            };
        }).ToList();
    }
}
