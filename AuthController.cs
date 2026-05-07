using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using JsbaiBackend.Models;

namespace JsbaiBackend.Controllers;

/// <summary>
/// Handles admin authentication.
///
/// HOW JWT WORKS (simple explanation):
/// 1. Admin enters password → sends to POST /api/auth/login
/// 2. Server checks password → if correct, creates a signed "token" (like a temporary ID card)
/// 3. Token is sent back to the browser
/// 4. Browser stores token and sends it with every future request
/// 5. Server verifies the token's signature — no need to check password again
/// 6. Token expires after 8 hours — admin must log in again
///
/// WHY THIS IS BETTER THAN PLAIN PASSWORD:
/// - Password never travels in requests after login
/// - Token can be invalidated/expired
/// - Even if token is stolen, it expires quickly
/// - Follows industry standard security practices
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IConfiguration config, ILogger<AuthController> logger)
    {
        _config = config;
        _logger = logger;
    }

    // ── POST /api/auth/login ───────────────────────────────────────────────
    /// <summary>
    /// Admin login. Accepts password, returns JWT token if correct.
    /// Rate limited to 5 attempts per minute (configured in Program.cs).
    /// </summary>
    [HttpPost("login")]
    public IActionResult Login([FromBody] LoginRequest request)
    {
        // Validate the password
        var correctPassword = _config["AdminPassword"];
        if (string.IsNullOrEmpty(request.Password) || request.Password != correctPassword)
        {
            // Log failed attempt with IP (useful for detecting brute force)
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            _logger.LogWarning("Failed login attempt from IP: {IP}", ip);

            // Always return 401 Unauthorized — don't say "wrong password"
            // (telling attacker the password format helps them)
            return Unauthorized(ApiResponse.Fail("Invalid credentials"));
        }

        // Password correct — generate JWT token
        var token = GenerateJwtToken();

        _logger.LogInformation("Admin login successful");
        return Ok(ApiResponse<object>.Ok(new { token, expiresIn = "8 hours" }));
    }

    // ── POST /api/auth/verify ──────────────────────────────────────────────
    /// <summary>
    /// Checks if a token is still valid. Used by admin panel on page load.
    /// </summary>
    [HttpGet("verify")]
    [Microsoft.AspNetCore.Authorization.Authorize]
    public IActionResult Verify() => Ok(ApiResponse.Ok("Token is valid"));

    // ── JWT Generation ─────────────────────────────────────────────────────
    private string GenerateJwtToken()
    {
        var jwtSettings = _config.GetSection("JwtSettings");
        var secret      = jwtSettings["Secret"] ?? throw new InvalidOperationException("JWT secret not configured");
        var issuer      = jwtSettings["Issuer"] ?? "jsbai-api";
        var audience    = jwtSettings["Audience"] ?? "jsbai-admin";
        var expiryHours = int.Parse(jwtSettings["ExpiryHours"] ?? "8");

        var key         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Claims are pieces of information embedded in the token
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "Admin"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
        };

        var token = new JwtSecurityToken(
            issuer:             issuer,
            audience:           audience,
            claims:             claims,
            expires:            DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

/// <summary>Request body for login</summary>
public class LoginRequest
{
    public string Password { get; set; } = string.Empty;
}
