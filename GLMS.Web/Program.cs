using GLMS.Web.Data;
using GLMS.Web.Services;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddHttpClient("GlmsApi", client =>
{
    client.BaseAddress = new Uri(
        builder.Configuration["ApiSettings:BaseUrl"]
        ?? "http://localhost:5148/");
});

builder.Services.AddScoped<ContractsApiClient>();

builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// --- Database (SQL Server via EF Core) ---
builder.Services.AddDbContext<GlmsDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("GlmsDb")));

// --- Infrastructure ---
builder.Services.AddMemoryCache();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<FormOptions>(o => o.MultipartBodyLengthLimit = AgreementFileValidator.MaxBytes + 1024 * 1024);

builder.Services.AddSingleton<IAgreementStorage>(_ =>
{
    var relative = builder.Configuration["FileStorage:RootPath"] ?? "App_Data/SignedAgreements";
    return new LocalFileStorage(Path.Combine(builder.Environment.ContentRootPath, relative));
});

// --- Currency: Strategy pattern. Order matters: the live API is tried first, then cached / fallback rates. ---
builder.Services.Configure<CurrencyOptions>(builder.Configuration.GetSection("Currency"));
builder.Services.AddHttpClient<LiveApiConversionStrategy>((sp, client) =>
{
    var options = builder.Configuration.GetSection("Currency").Get<CurrencyOptions>() ?? new CurrencyOptions();
    client.BaseAddress = new Uri(options.BaseUrl.EndsWith('/') ? options.BaseUrl : options.BaseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
});
builder.Services.AddTransient<ICurrencyConversionStrategy>(sp => sp.GetRequiredService<LiveApiConversionStrategy>());
builder.Services.AddTransient<ICurrencyConversionStrategy, CachedConversionStrategy>();
builder.Services.AddScoped<ICurrencyService, CurrencyService>();

// --- Business services ---
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();

var app = builder.Build();

// Create the database on first run (and optionally seed demo data).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GlmsDbContext>();
    if (app.Configuration.GetValue<bool>("Database:UseMigrations"))
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }

    if (app.Configuration.GetValue<bool>("Database:SeedDemoData"))
    {
        await SeedData.InitialiseAsync(db, scope.ServiceProvider.GetRequiredService<TimeProvider>().Today());
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// en-US so decimal inputs such as 100.50 always bind the same way regardless of the server's regional settings.
app.UseRequestLocalization("en-US");

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// Makes the entry point visible to integration tests.
public partial class Program
{
}
