
using GLMS.Api.DTOs;
using GLMS.Api.Services;
using GLMS.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContractsController : ControllerBase
{
    private readonly IContractService _contractService;

    public ContractsController(IContractService contractService)
    {
        _contractService = contractService;
    }

    // GET: api/Contracts
    [HttpGet]
    public async Task<IActionResult> GetContracts(
        [FromQuery] string? status,
        CancellationToken ct)
    {
        ContractStatus? parsedStatus = null;

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ContractStatus>(
                    status, true, out var value)
                || !Enum.IsDefined(value))
            {
                return BadRequest(new
                {
                    message = "Invalid contract status."
                });
            }

            parsedStatus = value;
        }

        var contracts = await _contractService.SearchAsync(
            new ContractSearchCriteria
            {
                Status = parsedStatus
            }, ct);

        return Ok(contracts.Select(c => new
        {
            c.Id,
            c.ClientId,
            c.StartDate,
            c.EndDate,
            c.Status,
            c.ServiceLevel,
            c.HasSignedAgreement,
            Client = c.Client == null ? null : new
            {
                c.Client.Id,
                c.Client.Name
            }
        }));
    }

    // GET: api/Contracts/5
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetContract(
        int id,
        CancellationToken ct)
    {
        var contract = await _contractService.GetAsync(id, ct);

        if (contract is null)
        {
            return NotFound(new
            {
                message = "Contract not found."
            });
        }

        return Ok(new
        {
            contract.Id,
            contract.ClientId,
            contract.StartDate,
            contract.EndDate,
            contract.Status,
            contract.ServiceLevel,
            contract.HasSignedAgreement,
            Client = contract.Client == null ? null : new
            {
                contract.Client.Id,
                contract.Client.Name
            },
            ServiceRequests = contract.ServiceRequests
        });
    }

    // POST: api/Contracts
    [HttpPost]
    public async Task<IActionResult> CreateContract(
        [FromBody] CreateContractDto dto,
        CancellationToken ct)
    {
        var contract = new Contract
        {
            ClientId = dto.ClientId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ServiceLevel = dto.ServiceLevel,
            Status = ContractStatus.Draft
        };

        try
        {
            var created = await _contractService.CreateAsync(
                contract, ct);

            return CreatedAtAction(
                nameof(GetContract),
                new { id = created.Id },
                new
                {
                    created.Id,
                    created.ClientId,
                    created.StartDate,
                    created.EndDate,
                    created.ServiceLevel,
                    created.Status
                });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PUT: api/Contracts/5
    [HttpPut("{id:int}")]
    public async Task<IActionResult> EditContract(
        int id,
        [FromBody] Contract input,
        CancellationToken ct)
    {
        if (id != input.Id)
        {
            return BadRequest(new
            {
                message = "Contract ID mismatch."
            });
        }

        if (await _contractService.GetAsync(id, ct) is null)
        {
            return NotFound(new
            {
                message = "Contract not found."
            });
        }

        try
        {
            await _contractService.UpdateAsync(input, ct);
            return NoContent();
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // PATCH: api/Contracts/5/status
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateContractStatusDto dto,
        CancellationToken ct)
    {
        if (!Enum.IsDefined(dto.Status))
        {
            return BadRequest(new
            {
                message = "Invalid contract status."
            });
        }

        if (await _contractService.GetAsync(id, ct) is null)
        {
            return NotFound(new
            {
                message = "Contract not found."
            });
        }

        try
        {
            await _contractService.ChangeStatusAsync(
                id, dto.Status, ct);

            return Ok(new
            {
                id,
                Status = dto.Status,
                message = "Contract status updated successfully."
            });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST: api/Contracts/5/agreement
    [HttpPost("{id:int}/agreement")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024 + 1024)]
    public async Task<IActionResult> UploadAgreement(
        int id,
        IFormFile agreement,
        CancellationToken ct)
    {
        if (agreement is null || agreement.Length == 0)
        {
            return BadRequest(new
            {
                message = "Please select a PDF agreement."
            });
        }

        if (await _contractService.GetAsync(id, ct) is null)
        {
            return NotFound(new
            {
                message = "Contract not found."
            });
        }

        try
        {
            await using var stream = agreement.OpenReadStream();

            await _contractService.AttachAgreementAsync(
                id,
                agreement.FileName,
                agreement.Length,
                stream,
                ct);

            return Ok(new
            {
                message = "Signed agreement uploaded successfully."
            });
        }
        catch (InvalidFileException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (BusinessRuleException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // GET: api/Contracts/5/agreement
    [HttpGet("{id:int}/agreement")]
    public async Task<IActionResult> DownloadAgreement(
        int id,
        CancellationToken ct)
    {
        var download = await _contractService.OpenAgreementAsync(
            id, ct);

        if (download is null)
        {
            return NotFound(new
            {
                message = "No signed agreement was found."
            });
        }

        return File(
            download.Content,
            "application/pdf",
            download.FileName);
    }
}
