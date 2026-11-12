
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GLMS.Web.Controllers;

public class AccountController : Controller
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AccountController(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        using var client = _httpClientFactory.CreateClient("GlmsApi");

        using var response = await client.PostAsJsonAsync(
            "api/Auth/login",
            new { username, password },
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            ModelState.AddModelError(
                "", "Invalid username or password.");
            return View();
        }

        if (!response.IsSuccessStatusCode)
        {
            ModelState.AddModelError(
                "", "Login is temporarily unavailable.");
            return View();
        }

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>(
            cancellationToken: cancellationToken);

        if (string.IsNullOrWhiteSpace(result?.Token))
        {
            ModelState.AddModelError(
                "", "The API did not return an access token.");
            return View();
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "Admin")
        };

        var identity = new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        var principal = new ClaimsPrincipal(identity);

        var properties = new AuthenticationProperties
        {
            IsPersistent = false,
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
        };

        properties.StoreTokens(new[]
        {
            new AuthenticationToken
            {
                Name = "access_token",
                Value = result.Token
            }
        });

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);

        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(
            CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction("Login");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return StatusCode(403, "Access denied.");
    }

    private sealed record LoginResponse(string Token);
}
