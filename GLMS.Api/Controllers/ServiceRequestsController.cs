using GLMS.Api.Services;
using Microsoft.AspNetCore.Authorization;
using GLMS.Shared.Models;
using Microsoft.AspNetCore.Mvc;
namespace GLMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ServiceRequestsController : ControllerBase
{
    private readonly IServiceRequestService _requests;
    private readonly ICurrencyService _currency;
    public ServiceRequestsController(IServiceRequestService requests, ICurrencyService currency) { _requests = requests; _currency = currency; }
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int? contractId, CancellationToken ct) => Ok((await _requests.ListAsync(contractId, ct)).Select(Project));
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) { var r = await _requests.GetAsync(id, ct); return r is null ? NotFound() : Ok(Project(r)); }
    public record CreateRequest(int ContractId, string Description, decimal Amount, string CurrencyCode);
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRequest input, CancellationToken ct)
    {
        try { var r = await _requests.CreateAsync(input.ContractId, input.Description, input.Amount, input.CurrencyCode, ct); return CreatedAtAction(nameof(Get), new { id = r.Id }, Project(r)); }
        catch (BusinessRuleException ex) { return BadRequest(new { message = ex.Message }); }
    }
    public record StatusRequest(ServiceRequestStatus Status);
    [HttpPatch("{id:int}/status")]
    public async Task<IActionResult> Status(int id, [FromBody] StatusRequest input, CancellationToken ct)
    {
        try { await _requests.ChangeStatusAsync(id, input.Status, ct); return NoContent(); }
        catch (BusinessRuleException ex) { return BadRequest(new { message = ex.Message }); }
    }
    [HttpGet("rate")]
    public async Task<IActionResult> Rate([FromQuery] string currency = "USD", CancellationToken ct = default)
    {
        try { var rate = await _currency.GetRateToZarAsync(currency, ct); return Ok(new { currency = rate.Currency, rate = rate.RateToZar, source = rate.Source, isFallback = rate.IsFallback, retrievedAtUtc = rate.RetrievedAtUtc }); }
        catch (CurrencyUnavailableException ex) { return StatusCode(503, new { error = ex.Message }); }
        catch (BusinessRuleException ex) { return BadRequest(new { error = ex.Message }); }
    }
    private static object Project(ServiceRequest r) => new { r.Id, r.ContractId, r.Description, r.OriginalAmount, r.CurrencyCode, r.ExchangeRate, r.RateSource, r.CostZar, r.Status, r.CreatedAtUtc, Contract = r.Contract is null ? null : new { r.Contract.Id, r.Contract.ClientId, r.Contract.StartDate, r.Contract.EndDate, r.Contract.Status, r.Contract.ServiceLevel, Client = r.Contract.Client is null ? null : new { r.Contract.Client.Id, r.Contract.Client.Name } } };
}
