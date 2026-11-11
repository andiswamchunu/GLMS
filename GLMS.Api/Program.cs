
using GLMS.Api.Data;
using GLMS.Api.Services;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// Register business services
builder.Services.AddScoped<IContractService, ContractService>();
builder.Services.AddScoped<IServiceRequestService, ServiceRequestService>();

// Register agreement file storage

builder.Services.AddScoped<IAgreementStorage>(sp =>
{
    var environment = sp.GetRequiredService<IWebHostEnvironment>();

    var storagePath = Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "SignedAgreements");

    return new LocalFileStorage(storagePath);
});


// Register time provider
builder.Services.AddSingleton(TimeProvider.System);

// Register MVC API controllers
builder.Services.AddControllers();

// Register SQL Server database
builder.Services.AddDbContext<GlmsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("GlmsDb")));

// Enable Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


// Currency configuration
builder.Services.AddMemoryCache();

builder.Services.Configure<CurrencyOptions>(
    builder.Configuration.GetSection("Currency"));

// Register currency service
builder.Services.AddScoped<ICurrencyService, CurrencyService>();

// Live currency conversion strategy
builder.Services.AddHttpClient<LiveApiConversionStrategy>(client =>
{
    client.BaseAddress = new Uri("https://open.er-api.com/v6/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<ICurrencyConversionStrategy>(sp =>
    sp.GetRequiredService<LiveApiConversionStrategy>());

// Cached fallback strategy
builder.Services.AddScoped<ICurrencyConversionStrategy,
    CachedConversionStrategy>();


// Register currency conversion strategies
builder.Services.AddHttpClient<LiveApiConversionStrategy>();

builder.Services.AddScoped<ICurrencyConversionStrategy>(
    sp => sp.GetRequiredService<LiveApiConversionStrategy>());

builder.Services.AddScoped<ICurrencyConversionStrategy,
    CachedConversionStrategy>();


builder.Services.AddMemoryCache();

builder.Services.Configure<CurrencyOptions>(
    builder.Configuration.GetSection("Currency"));

builder.Services.AddScoped<ICurrencyService, CurrencyService>();

builder.Services.AddScoped<ICurrencyConversionStrategy,
    CachedConversionStrategy>();


var app = builder.Build();

// Enable Swagger during development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
