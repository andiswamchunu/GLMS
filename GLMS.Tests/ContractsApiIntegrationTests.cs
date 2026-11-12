
using System.Net;
using System.Net.Http.Json;
using GLMS.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

// Explicitly reference the API entry point
using ApiProgram = GLMS.Api.Program;

namespace GLMS.Tests;

public class ContractsApiIntegrationTests
{
    private sealed class TestApiFactory : WebApplicationFactory<ApiProgram>
    {
        protected override void ConfigureWebHost(
            IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");

            builder.ConfigureServices(services =>
            {
                // Remove SQL Server registration
                services.RemoveAll<DbContextOptions<GlmsDbContext>>();
                services.RemoveAll<GlmsDbContext>();

                // Use an isolated in-memory database
                services.AddDbContext<GlmsDbContext>(options =>
                {
                    options.UseInMemoryDatabase(
                        Guid.NewGuid().ToString());
                });
            });
        }
    }

    [Fact]
    public async Task GetContracts_ReturnsOk()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/Contracts");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetContracts_WhenEmpty_ReturnsEmptyList()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var contracts = await client.GetFromJsonAsync<
            List<System.Text.Json.JsonElement>>(
                "/api/Contracts");

        Assert.NotNull(contracts);
        Assert.Empty(contracts);
    }

    [Fact]
    public async Task GetContract_UnknownId_ReturnsNotFound()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/Contracts/99999");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task GetContracts_InvalidStatus_ReturnsBadRequest()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/Contracts?status=InvalidStatus");

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task CreateContract_UnknownClient_ReturnsBadRequest()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        var request = new
        {
            ClientId = 99999,
            StartDate = new DateTime(2026, 11, 12),
            EndDate = new DateTime(2027, 11, 12),
            ServiceLevel = 0
        };

        using var response = await client.PostAsJsonAsync(
            "/api/Contracts", request);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            response.StatusCode);
    }

    [Fact]
    public async Task ChangeStatus_UnknownContract_ReturnsNotFound()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            "/api/Contracts/99999/status")
        {
            Content = JsonContent.Create(new
            {
                Status = 1
            })
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }

    [Fact]
    public async Task DownloadAgreement_UnknownContract_ReturnsNotFound()
    {
        using var factory = new TestApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/api/Contracts/99999/agreement");

        Assert.Equal(
            HttpStatusCode.NotFound,
            response.StatusCode);
    }
}
