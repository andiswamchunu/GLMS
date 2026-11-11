using GLMS.Api.Data;
using GLMS.Shared.Models;
using Microsoft.EntityFrameworkCore;


namespace GLMS.Api.Services;

public sealed record AgreementDownload(Stream Content, string FileName);

public interface IContractService
{
    Task<IReadOnlyList<Contract>> SearchAsync(ContractSearchCriteria criteria, CancellationToken ct = default);
    Task<Contract?> GetAsync(int id, CancellationToken ct = default);
    Task<Contract> CreateAsync(Contract contract, CancellationToken ct = default);
    Task UpdateAsync(Contract contract, CancellationToken ct = default);
    Task ChangeStatusAsync(int id, ContractStatus newStatus, CancellationToken ct = default);
    Task<int> ExpireOverdueContractsAsync(CancellationToken ct = default);
    Task AttachAgreementAsync(int contractId, string fileName, long length, Stream content, CancellationToken ct = default);
    Task<AgreementDownload?> OpenAgreementAsync(int contractId, CancellationToken ct = default);
}

public sealed class ContractService : IContractService
{
    private readonly GlmsDbContext _db;
    private readonly IAgreementStorage _storage;
    private readonly TimeProvider _time;

    public ContractService(GlmsDbContext db, IAgreementStorage storage, TimeProvider time)
    {
        _db = db;
        _storage = storage;
        _time = time;
    }

    public async Task<IReadOnlyList<Contract>> SearchAsync(ContractSearchCriteria criteria, CancellationToken ct = default)
    {
        // Keep stored statuses honest before filtering, so "Expired" searches are accurate.
        await ExpireOverdueContractsAsync(ct);

        return await _db.Contracts
            .AsNoTracking()
            .Include(c => c.Client)
            .ApplyFilter(criteria)
            .ToListAsync(ct);
    }

    public Task<Contract?> GetAsync(int id, CancellationToken ct = default) =>
        _db.Contracts
            .Include(c => c.Client)
            .Include(c => c.ServiceRequests)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

    public async Task<Contract> CreateAsync(Contract contract, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        if (!await _db.Clients.AnyAsync(c => c.Id == contract.ClientId, ct))
        {
            throw new BusinessRuleException("The selected client does not exist.");
        }

        Normalise(contract);

        // A contract that already ended can only be recorded as Expired.
        if ((contract.Status is ContractStatus.Active or ContractStatus.OnHold) && contract.EndDate < _time.Today())
        {
            contract.Status = ContractStatus.Expired;
        }

        _db.Contracts.Add(contract);
        await _db.SaveChangesAsync(ct);
        return contract;
    }

    public async Task UpdateAsync(Contract contract, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(contract);

        var existing = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contract.Id, ct)
                       ?? throw new BusinessRuleException("The contract no longer exists.");

        if (!await _db.Clients.AnyAsync(c => c.Id == contract.ClientId, ct))
        {
            throw new BusinessRuleException("The selected client does not exist.");
        }

        Normalise(contract);

        existing.ClientId = contract.ClientId;
        existing.StartDate = contract.StartDate;
        existing.EndDate = contract.EndDate;
        existing.ServiceLevel = contract.ServiceLevel;
        // Status is deliberately not copied: it only changes through ChangeStatusAsync so the workflow is enforced.

        await _db.SaveChangesAsync(ct);
    }

    public async Task ChangeStatusAsync(int id, ContractStatus newStatus, CancellationToken ct = default)
    {
        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == id, ct)
                       ?? throw new BusinessRuleException("The contract no longer exists.");

        if (contract.Status == newStatus)
        {
            return;
        }

        if (!ContractRules.CanTransition(contract.Status, newStatus))
        {
            throw new BusinessRuleException($"A contract cannot move from {contract.Status} to {newStatus}.");
        }

        if (newStatus == ContractStatus.Active && contract.EndDate.Date < _time.Today())
        {
            throw new BusinessRuleException("This contract's end date has passed, so it cannot be activated. Edit the dates first.");
        }

        contract.Status = newStatus;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> ExpireOverdueContractsAsync(CancellationToken ct = default)
    {
        var today = _time.Today();

        var overdue = await _db.Contracts
            .Where(c => (c.Status == ContractStatus.Active || c.Status == ContractStatus.OnHold) && c.EndDate < today)
            .ToListAsync(ct);

        foreach (var contract in overdue)
        {
            contract.Status = ContractStatus.Expired;
        }

        if (overdue.Count > 0)
        {
            await _db.SaveChangesAsync(ct);
        }

        return overdue.Count;
    }

    public async Task AttachAgreementAsync(int contractId, string fileName, long length, Stream content, CancellationToken ct = default)
    {
        // Throws InvalidFileException for anything that is not a real PDF; nothing is stored in that case.
        AgreementFileValidator.Validate(fileName, length, content);

        var contract = await _db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId, ct)
                       ?? throw new BusinessRuleException("The contract no longer exists.");

        var previous = contract.SignedAgreementStoredName;
        var storedName = await _storage.SaveAsync(content, ct);

        contract.SignedAgreementStoredName = storedName;
        contract.SignedAgreementOriginalName = Path.GetFileName(fileName);
        contract.SignedAgreementUploadedAtUtc = _time.GetUtcNow().UtcDateTime;

        try
        {
            await _db.SaveChangesAsync(ct);
        }
        catch
        {
            _storage.Delete(storedName); // do not leave an orphaned file behind
            throw;
        }

        if (!string.IsNullOrEmpty(previous))
        {
            _storage.Delete(previous); // replaced agreement
        }
    }

    public async Task<AgreementDownload?> OpenAgreementAsync(int contractId, CancellationToken ct = default)
    {
        var contract = await _db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId, ct);
        if (contract is null || string.IsNullOrEmpty(contract.SignedAgreementStoredName))
        {
            return null;
        }

        var stream = _storage.OpenRead(contract.SignedAgreementStoredName);
        if (stream is null)
        {
            return null;
        }

        var name = string.IsNullOrWhiteSpace(contract.SignedAgreementOriginalName)
            ? $"contract-{contractId}-signed-agreement.pdf"
            : contract.SignedAgreementOriginalName;

        return new AgreementDownload(stream, name);
    }

    private static void Normalise(Contract contract)
    {
        contract.StartDate = contract.StartDate.Date;
        contract.EndDate = contract.EndDate.Date;

        if (contract.EndDate <= contract.StartDate)
        {
            throw new BusinessRuleException("The end date must be after the start date.");
        }
    }
}
