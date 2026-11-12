
using GLMS.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;

var builder = WebApplication.CreateBuilder(args);

// --- Cookie Authentication ---
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
    });

// --- JWT Token Forwarding ---
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<JwtTokenHandler>();

// --- Backend API Connection ---
builder.Services.AddHttpClient("GlmsApi", client =>
{
    var baseUrl =
        builder.Configuration["ApiSettings:BaseUrl"]
        ?? builder.Configuration["ApiBaseUrl"]
        ?? "http://localhost:5148/";

    client.BaseAddress = new Uri(baseUrl);
})
.AddHttpMessageHandler<JwtTokenHandler>();

// --- API Clients ---
builder.Services.AddScoped<ContractsApiClient>();
builder.Services.AddScoped<ClientsApiClient>();

// --- MVC Controllers and Authorization ---
builder.Services.AddControllersWithViews(options =>
{
    // Protect against cross-site request forgery
    options.Filters.Add(
        new AutoValidateAntiforgeryTokenAttribute());

    // Require authentication for MVC pages by default
    options.Filters.Add(
        new AuthorizeFilter());
});

// --- Infrastructure ---
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<FormOptions>(options =>
{
    options.MultipartBodyLengthLimit =
        AgreementFileValidator.MaxBytes + 1024 * 1024;
});

builder.Services.AddSingleton<IAgreementStorage>(_ =>
{
    var relative =
        builder.Configuration["FileStorage:RootPath"]
        ?? "App_Data/SignedAgreements";

    return new LocalFileStorage(
        Path.Combine(builder.Environment.ContentRootPath, relative));
});

// --- Currency Configuration ---
builder.Services.Configure<CurrencyOptions>(
    builder.Configuration.GetSection("Currency"));

// Live currency conversion
builder.Services.AddHttpClient<LiveApiConversionStrategy>((sp, client) =>
{
    var options =
        builder.Configuration
            .GetSection("Currency")
            .Get<CurrencyOptions>()
        ?? new CurrencyOptions();

    client.BaseAddress = new Uri(
        options.BaseUrl.EndsWith('/')
            ? options.BaseUrl
            : options.BaseUrl + "/");

    client.Timeout =
        TimeSpan.FromSeconds(options.TimeoutSeconds);
});

// Currency conversion strategies
builder.Services.AddTransient<ICurrencyConversionStrategy>(
    sp => sp.GetRequiredService<LiveApiConversionStrategy>());

builder.Services.AddTransient<
    ICurrencyConversionStrategy,
    CachedConversionStrategy>();

builder.Services.AddScoped<ICurrencyService, CurrencyService>();

// --- Business Services ---
builder.Services.AddScoped<
    IServiceRequestService,
    ApiServiceRequestService>();

var app = builder.Build();

// --- Error Handling ---
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// --- Localization ---
app.UseRequestLocalization("en-US");

// --- HTTP Middleware ---
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Authentication must run before authorization
app.UseAuthentication();
app.UseAuthorization();

// --- MVC Routes ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Makes the entry point visible to integration tests.
public partial class Program
{
}
