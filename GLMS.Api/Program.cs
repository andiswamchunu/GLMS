using GLMS.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using GLMS.Api.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// JWT authentication
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],

            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

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

// Register API controllers
builder.Services.AddControllers();

// Register SQL Server database
builder.Services.AddDbContext<GlmsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("GlmsDb")));

// Enable Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Currency configuration
builder.Services.AddMemoryCache();

builder.Services.Configure<CurrencyOptions>(
    builder.Configuration.GetSection("Currency"));

builder.Services.AddScoped<ICurrencyService, CurrencyService>();

// Live currency conversion strategy
builder.Services.AddHttpClient<LiveApiConversionStrategy>(client =>
{
    client.BaseAddress = new Uri("https://open.er-api.com/v6/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<ICurrencyConversionStrategy>(sp =>
    sp.GetRequiredService<LiveApiConversionStrategy>());

// Cached currency conversion strategy
builder.Services.AddScoped<
    ICurrencyConversionStrategy,
    CachedConversionStrategy>();

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

// Expose the API entry point for integration testing
namespace GLMS.Api
{
    public partial class Program
    {
    }
}
