using System.Diagnostics;
using System.Net.Http.Json;
using GLMS.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace GLMS.Web.Controllers;
public class HomeController : Controller
{
    private readonly HttpClient _api;
    public HomeController(IHttpClientFactory factory) => _api = factory.CreateClient("GlmsApi");
    public async Task<IActionResult> Index(CancellationToken ct) => View(await _api.GetFromJsonAsync<DashboardViewModel>("api/Dashboard", ct) ?? new DashboardViewModel());
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
