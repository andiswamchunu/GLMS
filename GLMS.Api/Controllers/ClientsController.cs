using GLMS.Api.Data;
using GLMS.Shared.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace GLMS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ClientsController : ControllerBase
{
    private readonly GlmsDbContext _db;
    public ClientsController(GlmsDbContext db) => _db = db;
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string? q, CancellationToken ct)
    {
        var query = _db.Clients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(c => c.Name.Contains(q.Trim()) || c.Region.Contains(q.Trim()));
        var clients = await query.OrderBy(c => c.Name).Select(c => new { c.Id, c.Name, c.ContactDetails, c.Region, Contracts = c.Contracts.Select(k => new { k.Id, k.ClientId, k.StartDate, k.EndDate, k.Status, k.ServiceLevel }).ToList() }).ToListAsync(ct);
        return Ok(clients);
    }
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var c = await _db.Clients.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.Name, x.ContactDetails, x.Region, Contracts = x.Contracts.Select(k => new { k.Id, k.ClientId, k.StartDate, k.EndDate, k.Status, k.ServiceLevel }).ToList() }).FirstOrDefaultAsync(ct);
        return c is null ? NotFound() : Ok(c);
    }
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Client input, CancellationToken ct)
    {
        var c = new Client { Name = input.Name, ContactDetails = input.ContactDetails, Region = input.Region };
        _db.Clients.Add(c); await _db.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(Get), new { id = c.Id }, c);
    }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] Client input, CancellationToken ct)
    {
        var c = await _db.Clients.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        c.Name = input.Name; c.ContactDetails = input.ContactDetails; c.Region = input.Region;
        await _db.SaveChangesAsync(ct); return NoContent();
    }
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var c = await _db.Clients.FindAsync(new object[] { id }, ct);
        if (c is null) return NotFound();
        if (await _db.Contracts.AnyAsync(k => k.ClientId == id, ct)) return Conflict(new { message = "This client has contracts and cannot be deleted." });
        _db.Clients.Remove(c); await _db.SaveChangesAsync(ct); return NoContent();
    }
}
