using System.Net;
using System.Net.Http.Json;
using GLMS.Shared.Models;
namespace GLMS.Web.Services;
public sealed class ApiServiceRequestService : IServiceRequestService
{
    private readonly HttpClient _http;
    public ApiServiceRequestService(IHttpClientFactory factory) => _http = factory.CreateClient("GlmsApi");
    public async Task<IReadOnlyList<ServiceRequest>> ListAsync(int? contractId = null, CancellationToken ct = default) => await _http.GetFromJsonAsync<List<ServiceRequest>>("api/ServiceRequests" + (contractId.HasValue ? $"?contractId={contractId}" : ""), ct) ?? new List<ServiceRequest>();
    public async Task<ServiceRequest?> GetAsync(int id, CancellationToken ct = default) { using var r = await _http.GetAsync($"api/ServiceRequests/{id}", ct); if (r.StatusCode == HttpStatusCode.NotFound) return null; r.EnsureSuccessStatusCode(); return await r.Content.ReadFromJsonAsync<ServiceRequest>(cancellationToken: ct); }
    public async Task<ServiceRequest> CreateAsync(int contractId, string description, decimal amount, string currencyCode, CancellationToken ct = default)
    {
        using var r = await _http.PostAsJsonAsync("api/ServiceRequests", new { contractId, description, amount, currencyCode }, ct);
        if (!r.IsSuccessStatusCode) throw new BusinessRuleException(await ErrorAsync(r, ct));
        return await r.Content.ReadFromJsonAsync<ServiceRequest>(cancellationToken: ct) ?? throw new InvalidOperationException("Empty API response.");
    }
    public async Task ChangeStatusAsync(int id, ServiceRequestStatus newStatus, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"api/ServiceRequests/{id}/status") { Content = JsonContent.Create(new { status = newStatus }) };
        using var r = await _http.SendAsync(request, ct);
        if (!r.IsSuccessStatusCode) throw new BusinessRuleException(await ErrorAsync(r, ct));
    }
    private static async Task<string> ErrorAsync(HttpResponseMessage r, CancellationToken ct)
    {
        try { using var doc = System.Text.Json.JsonDocument.Parse(await r.Content.ReadAsStringAsync(ct)); if (doc.RootElement.TryGetProperty("message", out var m)) return m.GetString() ?? "API request failed"; }
        catch (System.Text.Json.JsonException) { }
        return $"API request failed: {(int)r.StatusCode}";
    }
}
