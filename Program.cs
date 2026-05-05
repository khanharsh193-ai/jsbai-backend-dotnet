using Microsoft.EntityFrameworkCore;
using JsbaiBackend.Data;
using JsbaiBackend.Services;

/// <summary>
/// Program.cs is the ENTRY POINT of the entire application.
/// When you run the app, .NET starts here.
///
/// This file does two things:
/// 1. REGISTER SERVICES — tell .NET what classes are available (like a phone book)
/// 2. CONFIGURE PIPELINE — set up how requests flow through the app
/// </summary>

var builder = WebApplication.CreateBuilder(args);

// ══════════════════════════════════════════════════════════════════════════════
// SECTION 1 — REGISTER SERVICES
// Think of this as telling the app: "these are the tools you have available"
// ══════════════════════════════════════════════════════════════════════════════

// Add MVC Controllers — enables our SubmissionsController and AdminController
builder.Services.AddControllers();

// Add Swagger — generates automatic API documentation at /swagger
// Useful for testing your API endpoints without a frontend
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new()
    {
        Title = "JSBAI API",
        Version = "v1",
        Description = "Backend API for the Journal of Sustainable Biosciences & Agricultural Innovation"
    });
});

// Add SQLite Database
// EF Core will create a file called "jsbai.db" in the app folder — this IS the database
// No separate database server needed — the database is just one file
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? "Data Source=jsbai.db"));

// Register our custom services
// AddScoped means: create one instance per HTTP request
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileService, FileService>();

// Add CORS — Cross-Origin Resource Sharing
// This is what allows your GitHub Pages frontend to talk to this backend
// Without this, browsers block cross-origin requests for security reasons
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            // Allow requests from your GitHub Pages site
            .WithOrigins(
                "https://khanharsh193-ai.github.io",  // ← your GitHub Pages URL
                "http://localhost:3000",               // for local testing
                "http://localhost:5500"                // for VS Code Live Server
            )
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ══════════════════════════════════════════════════════════════════════════════
// SECTION 2 — BUILD THE APP
// ══════════════════════════════════════════════════════════════════════════════

var app = builder.Build();

// Auto-create the database and tables on startup
// This means you never have to manually set up the database — it just works
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// ══════════════════════════════════════════════════════════════════════════════
// SECTION 3 — CONFIGURE THE REQUEST PIPELINE
// Every HTTP request passes through these middleware layers in order
// Think of it like a series of checkpoints a request goes through
// ══════════════════════════════════════════════════════════════════════════════

// Show Swagger API docs (available in all environments for easy testing)
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "JSBAI API v1");
    c.RoutePrefix = "swagger";  // Access at: yourapp.com/swagger
});

// Serve static files (uploaded manuscripts) from wwwroot folder
app.UseStaticFiles();

// Apply CORS policy — must come before routing
app.UseCors("AllowFrontend");

// Route requests to the correct controller
app.UseRouting();
app.MapControllers();

// Root endpoint — useful for checking if the app is alive
app.MapGet("/", () => Results.Ok(new
{
    message = "JSBAI Backend API",
    version = "1.0",
    status  = "Running",
    docs    = "/swagger"
}));

app.Run();
