using System.Net;
using GLMS.Shared.Models;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Mvc;
namespace GLMS.Web.Controllers;
public class ClientsController : Controller
{
    private readonly ClientsApiClient _api;
    public ClientsController(ClientsApiClient api) => _api = api;
    public async Task<IActionResult> Index(string? q, CancellationToken ct) { ViewData["Query"] = q; return View(await _api.ListAsync(q, ct) ?? new List<Client>()); }
    public async Task<IActionResult> Details(int id, CancellationToken ct) { var c = await _api.GetAsync(id, ct); return c is null ? NotFound() : View(c); }
    public IActionResult Create() => View(new Client());
    [HttpPost]
    public async Task<IActionResult> Create([Bind("Name,ContactDetails,Region")] Client c, CancellationToken ct)
    { if (!ModelState.IsValid) return View(c); await _api.CreateAsync(c, ct); TempData["Success"] = $"Client '{c.Name}' was created."; return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> Edit(int id, CancellationToken ct) { var c = await _api.GetAsync(id, ct); return c is null ? NotFound() : View(c); }
    [HttpPost]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,ContactDetails,Region")] Client c, CancellationToken ct)
    { if (id != c.Id) return BadRequest(); if (!ModelState.IsValid) return View(c); await _api.UpdateAsync(id, c, ct); TempData["Success"] = $"Client '{c.Name}' was updated."; return RedirectToAction(nameof(Details), new { id }); }
    [HttpPost]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    { var status = await _api.DeleteAsync(id, ct); if (status == HttpStatusCode.NotFound) return NotFound(); TempData[status == HttpStatusCode.Conflict ? "Error" : "Success"] = status == HttpStatusCode.Conflict ? "This client still has contracts and cannot be deleted." : "Client deleted."; return RedirectToAction(nameof(Index)); }
}
