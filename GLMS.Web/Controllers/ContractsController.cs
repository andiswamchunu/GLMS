using System.Net;
using GLMS.Shared.Models;
using GLMS.Web.Services;
using GLMS.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GLMS.Web.Controllers;

public class ContractsController : Controller
{
    private readonly ContractsApiClient _api;
    private readonly ClientsApiClient _clients;
    private readonly TimeProvider _time;

    public ContractsController(
        ClientsApiClient clients,
        TimeProvider time,
        ContractsApiClient api)
    {
        _clients = clients;
        _time = time;
        _api = api;
    }

    // GET: /Contracts
    public async Task<IActionResult> Index(
        ContractListViewModel filter,
        CancellationToken ct)
    {
        if (filter.From.HasValue &&
            filter.To.HasValue &&
            filter.From > filter.To)
        {
            ModelState.AddModelError(
                nameof(filter.To),
                "The 'until' date must be on or after the 'from' date.");

            filter.Results = Array.Empty<Contract>();
        }
        else
        {
            var contracts = await _api.GetContractsAsync(
                filter.Status?.ToString(), ct);

            var query = contracts.AsEnumerable();

            if (filter.ClientId.HasValue)
            {
                query = query.Where(c =>
                    c.ClientId == filter.ClientId.Value);
            }

            if (filter.From.HasValue)
            {
                query = query.Where(c =>
                    c.EndDate >= filter.From.Value);
            }

            if (filter.To.HasValue)
            {
                query = query.Where(c =>
                    c.StartDate <= filter.To.Value);
            }

            filter.Results = query.ToList();
        }

        filter.Clients = await ClientListAsync(
            filter.ClientId, ct);

        return View(filter);
    }

    // GET: /Contracts/Details/5
    public async Task<IActionResult> Details(
        int id,
        CancellationToken ct)
    {
        var contract = await _api.GetContractAsync(id, ct);

        if (contract is null)
            return NotFound();

        var today = _time.Today();

        var model = new ContractDetailsViewModel
        {
            Contract = contract,

            CanCreateRequest =
                ContractRules.CanCreateServiceRequest(
                    contract, today),

            EffectiveStatus =
                ContractRules.EffectiveStatus(
                    contract, today),

            AllowedTransitions =
                Enum.GetValues<ContractStatus>()
                    .Where(s => ContractRules.CanTransition(
                        contract.Status, s))
                    .ToList()
        };

        return View(model);
    }

    // GET: /Contracts/Create
    public async Task<IActionResult> Create(
        int? clientId,
        CancellationToken ct)
    {
        ViewData["Clients"] =
            await ClientListAsync(clientId, ct);

        return View(new Contract
        {
            ClientId = clientId ?? 0
        });
    }

    // POST: /Contracts/Create
    [HttpPost]
    public async Task<IActionResult> Create(
        [Bind("ClientId,StartDate,EndDate,Status,ServiceLevel")]
        Contract contract,
        IFormFile? agreement,
        CancellationToken ct)
    {
        if (agreement is { Length: > 0 })
        {
            try
            {
                await using var probe = agreement.OpenReadStream();

                AgreementFileValidator.Validate(
                    agreement.FileName,
                    agreement.Length,
                    probe);
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
                var created = await _api.CreateContractAsync(
                    contract, ct);

                if (agreement is { Length: > 0 })
                {
                    try
                    {
                        await using var stream =
                            agreement.OpenReadStream();

                        await _api.UploadAgreementAsync(
                            created.Id,
                            agreement.FileName,
                            stream,
                            ct);
                    }
                    catch (HttpRequestException ex)
                    {
                        TempData["Error"] =
                            "The contract was created, but the agreement " +
                            "could not be uploaded: " + ex.Message;

                        return RedirectToAction(
                            nameof(Details),
                            new { id = created.Id });
                    }
                }

                TempData["Success"] = "The contract was created.";

                return RedirectToAction(
                    nameof(Details),
                    new { id = created.Id });
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode == HttpStatusCode.BadRequest ||
                      ex.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                ModelState.AddModelError(
                    string.Empty, ex.Message);
            }
        }

        ViewData["Clients"] =
            await ClientListAsync(contract.ClientId, ct);

        return View(contract);
    }

    // GET: /Contracts/Edit/5
    public async Task<IActionResult> Edit(
        int id,
        CancellationToken ct)
    {
        var contract = await _api.GetContractAsync(id, ct);

        if (contract is null)
            return NotFound();

        ViewData["Clients"] =
            await ClientListAsync(contract.ClientId, ct);

        return View(contract);
    }

    // POST: /Contracts/Edit/5
    [HttpPost]
    public async Task<IActionResult> Edit(
        int id,
        [Bind("Id,ClientId,StartDate,EndDate,ServiceLevel")]
        Contract contract,
        CancellationToken ct)
    {
        if (id != contract.Id)
            return BadRequest();

        if (ModelState.IsValid)
        {
            try
            {
                await _api.UpdateContractAsync(contract, ct);

                TempData["Success"] = "The contract was updated.";

                return RedirectToAction(
                    nameof(Details),
                    new { id });
            }
            catch (HttpRequestException ex)
                when (ex.StatusCode == HttpStatusCode.BadRequest ||
                      ex.StatusCode == HttpStatusCode.UnprocessableEntity)
            {
                ModelState.AddModelError(
                    string.Empty, ex.Message);
            }
        }

        var current = await _api.GetContractAsync(id, ct);

        if (current is null)
            return NotFound();

        contract.Status = current.Status;

        ViewData["Clients"] =
            await ClientListAsync(contract.ClientId, ct);

        return View(contract);
    }

    // POST: /Contracts/ChangeStatus/5
    [HttpPost]
    public async Task<IActionResult> ChangeStatus(
        int id,
        ContractStatus status,
        CancellationToken ct)
    {
        try
        {
            await _api.ChangeStatusAsync(id, status, ct);

            TempData["Success"] =
                $"The contract is now {status}.";
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.BadRequest ||
                  ex.StatusCode == HttpStatusCode.NotFound)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    // POST: /Contracts/UploadAgreement/5
    [HttpPost]
    public async Task<IActionResult> UploadAgreement(
        int id,
        IFormFile? agreement,
        CancellationToken ct)
    {
        if (agreement is null || agreement.Length == 0)
        {
            TempData["Error"] =
                "Please choose a PDF file to upload.";

            return RedirectToAction(
                nameof(Details),
                new { id });
        }

        try
        {
            await using var stream = agreement.OpenReadStream();

            await _api.UploadAgreementAsync(
                id,
                agreement.FileName,
                stream,
                ct);

            TempData["Success"] =
                "The signed agreement was uploaded.";
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.BadRequest ||
                  ex.StatusCode == HttpStatusCode.NotFound ||
                  ex.StatusCode == HttpStatusCode.RequestEntityTooLarge)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(
            nameof(Details),
            new { id });
    }

    // GET: /Contracts/DownloadAgreement/5
    public async Task<IActionResult> DownloadAgreement(
        int id,
        CancellationToken ct)
    {
        var download = await _api.DownloadAgreementAsync(id, ct);

        if (download is null)
            return NotFound();

        return File(
            download.Value.Content,
            "application/pdf",
            download.Value.FileName);
    }

    // Client dropdown remains database-backed for now.
    private async Task<SelectList> ClientListAsync(
        int? selected,
        CancellationToken ct)
    {
        var clients = (await _clients.ListAsync(null, ct) ?? new List<Client>()).OrderBy(c => c.Name).ToList();

        return new SelectList(
            clients,
            nameof(Client.Id),
            nameof(Client.Name),
            selected);
    }
}
