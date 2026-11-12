using System.Net;
using System.Net.Http.Json;
using GLMS.Shared.Models;
namespace GLMS.Web.Services;
public sealed class ClientsApiClient
{
    private readonly HttpClient _http;
    public ClientsApiClient(IHttpClientFactory factory) => _http = factory.CreateClient("GlmsApi");
    public Task<List<Client>?> ListAsync(string? q, CancellationToken ct) => _http.GetFromJsonAsync<List<Client>>("api/Clients" + (string.IsNullOrWhiteSpace(q) ? "" : "?q=" + Uri.EscapeDataString(q)), ct);
    public async Task<Client?> GetAsync(int id, CancellationToken ct) { using var r = await _http.GetAsync($"api/Clients/{id}", ct); if (r.StatusCode == HttpStatusCode.NotFound) return null; r.EnsureSuccessStatusCode(); return await r.Content.ReadFromJsonAsync<Client>(cancellationToken: ct); }
    public async Task CreateAsync(Client c, CancellationToken ct) { using var r = await _http.PostAsJsonAsync("api/Clients", c, ct); r.EnsureSuccessStatusCode(); }
    public async Task UpdateAsync(int id, Client c, CancellationToken ct) { using var r = await _http.PutAsJsonAsync($"api/Clients/{id}", c, ct); r.EnsureSuccessStatusCode(); }
    public async Task<HttpStatusCode> DeleteAsync(int id, CancellationToken ct) { using var r = await _http.DeleteAsync($"api/Clients/{id}", ct); if (r.StatusCode != HttpStatusCode.Conflict && r.StatusCode != HttpStatusCode.NotFound) r.EnsureSuccessStatusCode(); return r.StatusCode; }
}
