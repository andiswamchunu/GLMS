
using GLMS.Api.Data;
using GLMS.Api.DTOs;
using GLMS.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ContractsController : ControllerBase
{
    private readonly GlmsDbContext _context;

    public ContractsController(GlmsDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetContracts(
        [FromQuery] string? status)
    {
        var query = _context.Contracts
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<GLMS.Shared.Models.ContractStatus>(
                status, true, out var parsedStatus)
                || !Enum.IsDefined(parsedStatus))
            {
                return BadRequest("Invalid contract status.");
            }

            query = query.Where(c => c.Status == parsedStatus);
        }

        var contracts = await query
            .OrderBy(c => c.Id)
            .Select(c => new
            {
                c.Id,
                c.ClientId,
                c.StartDate,
                c.EndDate,
                c.Status,
                c.ServiceLevel
            })
            .ToListAsync();

        return Ok(contracts);
    }


    [HttpPost]
    public async Task<IActionResult> CreateContract(
        [FromBody] CreateContractDto dto)
    {
        // Confirm that the client exists
        bool clientExists = await _context.Clients
            .AnyAsync(c => c.Id == dto.ClientId);

        if (!clientExists)
        {
            return BadRequest(new
            {
                message = "The selected client does not exist."
            });
        }

        // Create a new contract
        var contract = new GLMS.Shared.Models.Contract
        {
            ClientId = dto.ClientId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            ServiceLevel = dto.ServiceLevel,
            Status = GLMS.Shared.Models.ContractStatus.Draft
        };

        // Save to SQL Server
        _context.Contracts.Add(contract);
        await _context.SaveChangesAsync();

        // Return the created contract
        return Created(
            $"/api/contracts/{contract.Id}",
            new
            {
                contract.Id,
                contract.ClientId,
                contract.StartDate,
                contract.EndDate,
                contract.ServiceLevel,
                contract.Status
            });
    }


    [HttpPatch("{id}/status")]
    public async Task<IActionResult> UpdateStatus(
        int id,
        [FromBody] UpdateContractStatusDto dto)
    {
        var contract = await _context.Contracts
            .FindAsync(id);

        if (contract == null)
        {
            return NotFound(new
            {
                message = "Contract not found."
            });
        }

        if (!Enum.IsDefined(dto.Status))
        {
            return BadRequest(new
            {
                message = "Invalid contract status."
            });
        }

        var currentStatus = contract.Status;
        var newStatus = dto.Status;

        bool allowed = (currentStatus, newStatus) switch
        {
            (ContractStatus.Draft, ContractStatus.Active) => true,
            (ContractStatus.Draft, ContractStatus.Expired) => true,
            (ContractStatus.Active, ContractStatus.OnHold) => true,
            (ContractStatus.Active, ContractStatus.Expired) => true,
            (ContractStatus.OnHold, ContractStatus.Active) => true,
            (ContractStatus.OnHold, ContractStatus.Expired) => true,
            _ => false
        };

        if (!allowed)
        {
            return BadRequest(new
            {
                message = $"Cannot change status from {currentStatus} to {newStatus}."
            });
        }

        contract.Status = newStatus;

        await _context.SaveChangesAsync();

        return Ok(new
        {
            contract.Id,
            contract.Status,
            message = "Contract status updated successfully."
        });
    }


}

