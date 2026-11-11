using GLMS.Web.Data;
using GLMS.Shared.Models;
using GLMS.Web.Services;
using GLMS.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Web.Controllers;

public class ContractsController : Controller
{
    private readonly GlmsDbContext _db;
    private readonly IContractService _contracts;
    private readonly TimeProvider _time;

    public ContractsController(GlmsDbContext db, IContractService contracts, TimeProvider time)
    {
        _db = db;
        _contracts = contracts;
        _time = time;
    }

    // GET /Contracts?From=2026-01-01&To=2026-03-31&Status=Active
    public async Task<IActionResult> Index(ContractListViewModel filter, CancellationToken ct)
    {
        if (filter.From.HasValue && filter.To.HasValue && filter.From > filter.To)
        {
            ModelState.AddModelError(nameof(filter.To), "The 'until' date must be on or after the 'from' date.");
            filter.Results = Array.Empty<Contract>();
        }
        else
        {
            filter.Results = await _contracts.SearchAsync(filter.ToCriteria(), ct);
        }

        filter.Clients = await ClientListAsync(filter.ClientId, ct);
        return View(filter);
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var contract = await _contracts.GetAsync(id, ct);
        if (contract is null)
        {
            return NotFound();
        }

        var today = _time.Today();
        var model = new ContractDetailsViewModel
        {
            Contract = contract,
            CanCreateRequest = ContractRules.CanCreateServiceRequest(contract, today),
            EffectiveStatus = ContractRules.EffectiveStatus(contract, today),
            AllowedTransitions = Enum.GetValues<ContractStatus>()
                .Where(s => ContractRules.CanTransition(contract.Status, s))
                .ToList()
        };
        return View(model);
    }

    public async Task<IActionResult> Create(int? clientId, CancellationToken ct)
    {
        ViewData["Clients"] = await ClientListAsync(clientId, ct);
        return View(new Contract { ClientId = clientId ?? 0 });
    }

    [HttpPost]
    public async Task<IActionResult> Create(
        [Bind("ClientId,StartDate,EndDate,Status,ServiceLevel")] Contract contract,
        IFormFile? agreement,
        CancellationToken ct)
    {
        if (agreement is { Length: > 0 })
        {
            // Validate up-front so a bad file does not leave a half-created contract behind.
            try
            {
                await using var probe = agreement.OpenReadStream();
                AgreementFileValidator.Validate(agreement.FileName, agreement.Length, probe);
            }
            catch (InvalidFileException ex)
            {
                ModelState.AddModelError("agreement", ex.Message);
            }
        }

        if (ModelState.IsValid)
        {
            try
            {
                await _contracts.CreateAsync(contract, ct);

                if (agreement is { Length: > 0 })
                {
                    await using var stream = agreement.OpenReadStream();
                    await _contracts.AttachAgreementAsync(contract.Id, agreement.FileName, agreement.Length, stream, ct);
                }

                TempData["Success"] = "The contract was created.";
                return RedirectToAction(nameof(Details), new { id = contract.Id });
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        ViewData["Clients"] = await ClientListAsync(contract.ClientId, ct);
        return View(contract);
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var contract = await _db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (contract is null)
        {
            return NotFound();
        }

        ViewData["Clients"] = await ClientListAsync(contract.ClientId, ct);
        return View(contract);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,ClientId,StartDate,EndDate,ServiceLevel")] Contract contract,
        CancellationToken ct)
    {
        if (id != contract.Id)
        {
            return BadRequest();
        }

        if (ModelState.IsValid)
        {
            try
            {
                await _contracts.UpdateAsync(contract, ct);
                TempData["Success"] = "The contract was updated.";
                return RedirectToAction(nameof(Details), new { id });
            }
            catch (BusinessRuleException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        var current = await _db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        if (current is null)
        {
            return NotFound();
        }

        contract.Status = current.Status; // shown read-only in the form
        ViewData["Clients"] = await ClientListAsync(contract.ClientId, ct);
        return View(contract);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeStatus(int id, ContractStatus status, CancellationToken ct)
    {
        try
        {
            await _contracts.ChangeStatusAsync(id, status, ct);
            TempData["Success"] = $"The contract is now {status}.";
        }
        catch (BusinessRuleException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> UploadAgreement(int id, IFormFile? agreement, CancellationToken ct)
    {
        if (agreement is null || agreement.Length == 0)
        {
            TempData["Error"] = "Please choose a PDF file to upload.";
            return RedirectToAction(nameof(Details), new { id });
        }

        try
        {
            await using var stream = agreement.OpenReadStream();
            await _contracts.AttachAgreementAsync(id, agreement.FileName, agreement.Length, stream, ct);
            TempData["Success"] = "The signed agreement was uploaded.";
        }
        catch (Exception ex) when (ex is InvalidFileException or BusinessRuleException)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /Contracts/DownloadAgreement/5
    public async Task<IActionResult> DownloadAgreement(int id, CancellationToken ct)
    {
        var download = await _contracts.OpenAgreementAsync(id, ct);
        if (download is null)
        {
            return NotFound();
        }

        return File(download.Content, "application/pdf", download.FileName);
    }

    private async Task<SelectList> ClientListAsync(int? selected, CancellationToken ct)
    {
        var clients = await _db.Clients.AsNoTracking().OrderBy(c => c.Name).ToListAsync(ct);
        return new SelectList(clients, nameof(Client.Id), nameof(Client.Name), selected);
    }
}
