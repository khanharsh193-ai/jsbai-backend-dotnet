using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using JsbaiBackend.Data;
using JsbaiBackend.Services;

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════════════════════════
// SECTION 1 — REGISTER SERVICES
// ══════════════════════════════════════════════════════════════════════════════

builder.Services.AddControllers();

// ── Swagger (API docs) — only shown in Development, HIDDEN in Production ──────
// This prevents attackers from seeing your API structure on the live server
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new() { Title = "JSBAI API", Version = "v1" });

        // Add JWT auth button to Swagger UI for easy testing
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name         = "Authorization",
            Type         = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
            Scheme       = "Bearer",
            BearerFormat = "JWT",
            Description  = "Enter your JWT token here"
        });
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
        {{
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id   = "Bearer"
                }
            },
            Array.Empty<string>()
        }});
    });
}

// ── Database ───────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=jsbai.db"));

// ── Custom Services ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IFileValidationService, FileValidationService>();  // ✅ NEW
builder.Services.AddScoped<ISanitizationService, SanitizationService>();      // ✅ NEW

// ── ✅ JWT Authentication ──────────────────────────────────────────────────────
// This tells .NET how to validate incoming JWT tokens
var jwtSettings  = builder.Configuration.GetSection("JwtSettings");
var jwtSecret    = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT Secret not configured in appsettings.json");
var jwtIssuer    = jwtSettings["Issuer"] ?? "jsbai-api";
var jwtAudience  = jwtSettings["Audience"] ?? "jsbai-admin";
var secretBytes  = Encoding.UTF8.GetBytes(jwtSecret);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,   // ← tokens expire after 8 hours
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtIssuer,
            ValidAudience            = jwtAudience,
            IssuerSigningKey         = new SymmetricSecurityKey(secretBytes),
            ClockSkew                = TimeSpan.FromMinutes(5), // 5 min tolerance for clock differences
        };

        // Return clean 401 JSON instead of HTML error page
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode  = 401;
                context.Response.ContentType = "application/json";
                return context.Response.WriteAsync("{\"success\":false,\"error\":\"Unauthorized — please log in\"}");
            }
        };
    });

builder.Services.AddAuthorization();

// ── ✅ Rate Limiting ───────────────────────────────────────────────────────────
// Automatically blocks too many requests from the same IP address.
// Think of it as a bouncer counting how many times the same person tries to enter.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429; // 429 = Too Many Requests

    // Policy for manuscript submissions: 5 per IP per hour
    // Prevents spam submissions
    options.AddPolicy("submissions", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit      = 5,
                Window           = TimeSpan.FromHours(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit       = 0,
            }));

    // Policy for submission tracking: 10 per IP per hour
    options.AddPolicy("tracking", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window      = TimeSpan.FromHours(1),
            }));

    // Policy for login attempts: 5 per IP per 15 minutes (prevents brute force)
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window      = TimeSpan.FromMinutes(15),
            }));

    // Policy for admin email: 20 per hour (prevents accidental bulk sending)
    options.AddPolicy("admin_email", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: "admin", // all admins share this limit
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window      = TimeSpan.FromHours(1),
            }));

    // Return JSON on rate limit (not HTML)
    options.OnRejected = async (context, _) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"success\":false,\"error\":\"Too many requests. Please wait before trying again.\"}");
    };
});

// ── ✅ Tightened CORS ──────────────────────────────────────────────────────────
// Only your GitHub Pages site is allowed to call this API.
// Any other website trying to call it will be blocked by the browser.
var allowedOrigins = builder.Configuration.GetSection("AllowedOrigins").Get<string[]>()
    ?? new[] { "https://khanharsh193-ai.github.io" };

builder.Services.AddCors(options =>
{
    options.AddPolicy("FrontendOnly", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .WithMethods("GET", "POST", "PATCH", "OPTIONS")  // Only methods we actually use
            .WithHeaders("Content-Type", "Authorization")    // Only headers we need
            .SetPreflightMaxAge(TimeSpan.FromHours(1));
    });
});

// ══════════════════════════════════════════════════════════════════════════════
// SECTION 2 — BUILD AND CONFIGURE THE APP
// ══════════════════════════════════════════════════════════════════════════════

var app = builder.Build();

// Auto-create database tables on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// ✅ Swagger only visible in Development (not on Railway live server)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "JSBAI API v1");
        c.RoutePrefix = "swagger";
    });
}

// ✅ Security headers — tells browsers to be strict about security
app.Use(async (context, next) =>
{
    // Prevents clickjacking attacks (embedding your site in iframes)
    context.Response.Headers.Append("X-Frame-Options", "DENY");

    // Prevents MIME type sniffing attacks
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");

    // Forces HTTPS
    context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");

    // Restricts what resources the page can load
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");

    await next();
});

app.UseStaticFiles();
app.UseCors("FrontendOnly");

// ✅ Rate limiting middleware
app.UseRateLimiter();

app.UseRouting();

// ✅ Authentication must come before Authorization
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Root health check
app.MapGet("/", () => Results.Ok(new
{
    message = "JSBAI API",
    status  = "Running",
    version = "2.0-secured"
}));

app.Run();
