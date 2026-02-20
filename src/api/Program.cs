using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using api.Data;
using api.Services;

var builder = WebApplication.CreateBuilder(args);

// Database configuration
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<BillingDbContext>(options =>
    options.UseSqlServer(connectionString));

// Add services
builder.Services.AddScoped<IAuditService, AuditService>();

// CORS configuration for frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["FrontendUrl"] ?? "http://localhost:5173"
            )
            .AllowAnyMethod()
            .AllowAnyHeader()
            .WithExposedHeaders("X-Total-Count", "X-Page", "X-Page-Size");
    });
});

// Authentication - Azure AD or dev bypass
var devBypass = builder.Configuration.GetValue<bool>("DEV_BYPASS_AUTH");
if (!devBypass)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(builder.Configuration.GetSection("AzureAd"));
    
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("BillingReader", policy => policy.RequireRole("BillingReader", "BillingAnalyst", "BillingAdmin"));
        options.AddPolicy("BillingAnalyst", policy => policy.RequireRole("BillingAnalyst", "BillingAdmin"));
        options.AddPolicy("BillingAdmin", policy => policy.RequireRole("BillingAdmin"));
    });
}
else
{
    // Dev bypass - no authentication
    builder.Services.AddAuthorization();
}

// Controllers
builder.Services.AddControllers();

// Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Epic Billing Analytics API", Version = "v1" });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowFrontend");

if (!devBypass)
{
    app.UseAuthentication();
}
app.UseAuthorization();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
