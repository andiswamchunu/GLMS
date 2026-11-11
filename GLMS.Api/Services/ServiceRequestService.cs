using GLMS.Api.Data;
using GLMS.Shared.Models;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Api.Services;

public interface IServiceRequestService
{
    Task<IReadOnlyList<ServiceRequest>> ListAsync(int? contractId = null, CancellationToken ct = default);
    Task<ServiceRequest?> GetAsync(int id, CancellationToken ct = default);
    Task<ServiceRequest> CreateAsync(int contractId, string description, decimal amount, string currencyCode, CancellationToken ct = default);
    Task ChangeStatusAsync(int id, ServiceRequestStatus newStatus, CancellationToken ct = default);
}

public sealed class ServiceRequestService : IServiceRequestService
{
    private readonly GlmsDbContext _db;
    private readonly ICurrencyService _currency;
    private readonly TimeProvider _time;

    public ServiceRequestService(GlmsDbContext db, ICurrencyService currency, TimeProvider time)
    {
        _db = db;
        _currency = currency;
        _time = time;
    }

    public async Task<IReadOnlyList<ServiceRequest>> ListAsync(int? contractId = null, CancellationToken ct = default)
    {
        var query = _db.ServiceRequests
            .AsNoTracking()
            .Include(r => r.Contract!).ThenInclude(c => c.Client)
            .AsQueryable();

        if (contractId is { } id)
        {
            query = query.Where(r => r.ContractId == id);
        }

        return await query.OrderByDescending(r => r.CreatedAtUtc).ThenByDescending(r => r.Id).ToListAsync(ct);
    }

    public Task<ServiceRequest?> GetAsync(int id, CancellationToken ct = default) =>
        _db.ServiceRequests
            .Include(r => r.Contract!).ThenInclude(c => c.Client)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task<ServiceRequest> CreateAsync(
        int contractId, string description, decimal amount, string currencyCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            throw new BusinessRuleException("Please describe the service request.");
        }

        if (amount <= 0)
        {
            throw new BusinessRuleException("The amount must be greater than zero.");
        }

        var contract = await _db.Contracts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == contractId, ct)
                       ?? throw new BusinessRuleException("The selected contract does not exist.");

        // Workflow rule: no requests on Expired / On Hold (or otherwise not-active) contracts.
        var rule = ContractRules.CanCreateServiceRequest(contract, _time.Today());
        if (!rule.Allowed)
        {
            throw new BusinessRuleException(rule.Reason!);
        }

        // The rate is fetched and the ZAR cost calculated on the server, so the saved value never depends on the browser.
        var rate = await _currency.GetRateToZarAsync(currencyCode, ct);
        var costZar = CurrencyConverter.ConvertToZar(amount, rate.RateToZar);

        var request = new ServiceRequest
        {
            ContractId = contractId,
            Description = description.Trim(),
            OriginalAmount = amount,
            CurrencyCode = rate.Currency,
            ExchangeRate = rate.RateToZar,
            RateSource = rate.Source,
            CostZar = costZar,
            Status = ServiceRequestStatus.Pending,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime
        };

        _db.ServiceRequests.Add(request);
        await _db.SaveChangesAsync(ct);
        return request;
    }

    public async Task ChangeStatusAsync(int id, ServiceRequestStatus newStatus, CancellationToken ct = default)
    {
        var request = await _db.ServiceRequests.FirstOrDefaultAsync(r => r.Id == id, ct)
                      ?? throw new BusinessRuleException("The service request no longer exists.");

        if (request.Status == newStatus)
        {
            return;
        }

        if (!ServiceRequestRules.CanTransition(request.Status, newStatus))
        {
            throw new BusinessRuleException($"A service request cannot move from {request.Status} to {newStatus}.");
        }

        request.Status = newStatus;
        await _db.SaveChangesAsync(ct);
    }
}
