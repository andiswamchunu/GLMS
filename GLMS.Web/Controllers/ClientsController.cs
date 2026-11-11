using GLMS.Web.Data;
using GLMS.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GLMS.Web.Controllers;

public class ClientsController : Controller
{
    private readonly GlmsDbContext _db;

    public ClientsController(GlmsDbContext db) => _db = db;

    public async Task<IActionResult> Index(string? q, CancellationToken ct)
    {
        var query = _db.Clients.AsNoTracking().Include(c => c.Contracts).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            query = query.Where(c => c.Name.Contains(term) || c.Region.Contains(term));
        }

        ViewData["Query"] = q;
        return View(await query.OrderBy(c => c.Name).ToListAsync(ct));
    }

    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var client = await _db.Clients.AsNoTracking()
            .Include(c => c.Contracts)
            .FirstOrDefaultAsync(c => c.Id == id, ct);
        return client is null ? NotFound() : View(client);
    }

    public IActionResult Create() => View(new Client());

    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,ContactDetails,Region")] Client client, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return View(client);
        }

        _db.Clients.Add(client);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = $"Client '{client.Name}' was created.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id, CancellationToken ct)
    {
        var client = await _db.Clients.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, ct);
        return client is null ? NotFound() : View(client);
    }

    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,ContactDetails,Region")] Client client, CancellationToken ct)
    {
        if (id != client.Id)
        {
            return BadRequest();
        }

        if (!ModelState.IsValid)
        {
            return View(client);
        }

        var existing = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (existing is null)
        {
            return NotFound();
        }

        existing.Name = client.Name;
        existing.ContactDetails = client.ContactDetails;
        existing.Region = client.Region;
        await _db.SaveChangesAsync(ct);

        TempData["Success"] = $"Client '{existing.Name}' was updated.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var client = await _db.Clients.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (client is null)
        {
            return NotFound();
        }

        if (await _db.Contracts.AnyAsync(c => c.ClientId == id, ct))
        {
            TempData["Error"] = $"'{client.Name}' still has contracts and cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        _db.Clients.Remove(client);
        await _db.SaveChangesAsync(ct);
        TempData["Success"] = $"Client '{client.Name}' was deleted.";
        return RedirectToAction(nameof(Index));
    }
}
