
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GLMS.Shared.Models;

namespace GLMS.Web.Services;

public class ContractsApiClient
{
    private readonly HttpClient _http;

    public ContractsApiClient(IHttpClientFactory factory)
    {
        _http = factory.CreateClient("GlmsApi");
    }

    public async Task<List<Contract>> GetContractsAsync(
        string? status = null,
        CancellationToken ct = default)
    {
        var url = "api/Contracts";

        if (!string.IsNullOrWhiteSpace(status))
        {
            url += "?status=" + Uri.EscapeDataString(status);
        }

        return await _http.GetFromJsonAsync<List<Contract>>(
            url, ct) ?? new List<Contract>();
    }

    public async Task<Contract?> GetContractAsync(
        int id,
        CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(
            $"api/Contracts/{id}", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<Contract>(
            cancellationToken: ct);
    }

    public async Task<Contract> CreateContractAsync(
        Contract contract,
        CancellationToken ct = default)
    {
        var request = new
        {
            contract.ClientId,
            contract.StartDate,
            contract.EndDate,
            contract.ServiceLevel
        };

        using var response = await _http.PostAsJsonAsync(
            "api/Contracts", request, ct);

        await EnsureSuccessAsync(response, ct);

        return await response.Content.ReadFromJsonAsync<Contract>(
            cancellationToken: ct)
            ?? throw new InvalidOperationException(
                "The API returned an empty contract response.");
    }

    public async Task UpdateContractAsync(
        Contract contract,
        CancellationToken ct = default)
    {
        var request = new
        {
            contract.Id,
            contract.ClientId,
            contract.StartDate,
            contract.EndDate,
            contract.ServiceLevel
        };

        using var response = await _http.PutAsJsonAsync(
            $"api/Contracts/{contract.Id}", request, ct);

        await EnsureSuccessAsync(response, ct);
    }

    public async Task ChangeStatusAsync(
        int id,
        ContractStatus status,
        CancellationToken ct = default)
    {
        var request = new { Status = status };

        using var message = new HttpRequestMessage(
            HttpMethod.Patch, $"api/Contracts/{id}/status")
        {
            Content = JsonContent.Create(request)
        };

        using var response = await _http.SendAsync(message, ct);

        await EnsureSuccessAsync(response, ct);
    }

    public async Task UploadAgreementAsync(
        int id,
        string fileName,
        Stream fileStream,
        CancellationToken ct = default)
    {
        using var form = new MultipartFormDataContent();
        using var fileContent = new StreamContent(fileStream);

        fileContent.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue(
                "application/pdf");

        form.Add(fileContent, "agreement", fileName);

        using var response = await _http.PostAsync(
            $"api/Contracts/{id}/agreement", form, ct);

        await EnsureSuccessAsync(response, ct);
    }

    public async Task<(byte[] Content, string FileName)?>
        DownloadAgreementAsync(
            int id,
            CancellationToken ct = default)
    {
        using var response = await _http.GetAsync(
            $"api/Contracts/{id}/agreement", ct);

        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessAsync(response, ct);

        var content = await response.Content.ReadAsByteArrayAsync(ct);

        var fileName =
            response.Content.Headers.ContentDisposition?
                .FileNameStar
            ?? response.Content.Headers.ContentDisposition?
                .FileName?.Trim('"')
            ?? $"contract-{id}-signed-agreement.pdf";

        return (content, fileName);
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;

        var message = await ReadErrorAsync(response, ct);

        throw new HttpRequestException(
            message, null, response.StatusCode);
    }

    private static async Task<string> ReadErrorAsync(
        HttpResponseMessage response,
        CancellationToken ct)
    {
        var content = await response.Content.ReadAsStringAsync(ct);

        if (string.IsNullOrWhiteSpace(content))
            return $"API request failed ({(int)response.StatusCode}).";

        try
        {
            using var document = JsonDocument.Parse(content);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) &&
                errors.ValueKind == JsonValueKind.Object)
            {
                var messages = new List<string>();

                foreach (var property in errors.EnumerateObject())
                {
                    if (property.Value.ValueKind !=
                        JsonValueKind.Array)
                        continue;

                    foreach (var error in property.Value.EnumerateArray())
                    {
                        if (error.ValueKind == JsonValueKind.String)
                        {
                            var value = error.GetString();
                            if (!string.IsNullOrWhiteSpace(value))
                                messages.Add(value);
                        }
                    }
                }

                if (messages.Count > 0)
                    return string.Join(" ", messages);
            }

            if (root.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                return message.GetString() ?? "API request failed.";
            }

            if (root.TryGetProperty("title", out var title) &&
                title.ValueKind == JsonValueKind.String)
            {
                return title.GetString() ?? "API request failed.";
            }
        }
        catch (JsonException)
        {
            // Use the HTTP status if the response is not JSON.
        }

        return $"API request failed ({(int)response.StatusCode}): " +
               response.ReasonPhrase;
    }
}
