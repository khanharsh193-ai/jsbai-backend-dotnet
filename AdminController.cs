using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using JsbaiBackend.Data;
using JsbaiBackend.DTOs;
using JsbaiBackend.Models;
using JsbaiBackend.Services;

namespace JsbaiBackend.Controllers;

/// <summary>
/// Admin endpoints — all protected by [Authorize].
///
/// [Authorize] means: the request MUST include a valid JWT token
/// in the Authorization header, like:
///   Authorization: Bearer eyJhbGciOiJIUzI1NiIs...
///
/// If the token is missing, expired, or tampered with,
/// .NET automatically rejects the request with 401 Unauthorized
/// before our code even runs.
///
/// This is much stronger than the old plain-text password header.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]   // ← ALL endpoints in this controller require valid JWT
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;
    private readonly IEmailService _emailService;
    private readonly ISanitizationService _sanitizer;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext db,
        IFileService fileService,
        IEmailService emailService,
        ISanitizationService sanitizer,
        ILogger<AdminController> logger)
    {
        _db = db;
        _fileService = fileService;
        _emailService = emailService;
        _sanitizer = sanitizer;
        _logger = logger;
    }

    // ── GET /api/admin/submissions ─────────────────────────────────────────
    [HttpGet("submissions")]
    public async Task<IActionResult> GetAll()
    {
        var submissions = await _db.Submissions
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new
            {
                s.Id, s.RefId, s.SubmittedAt, s.Title, s.ArticleType, s.SubjectArea,
                s.Keywords, s.FirstName, s.LastName, s.Email, s.Orcid,
                s.Affiliation, s.CoAuthors, s.Funding, s.SuggestedReviewers,
                s.Status, s.EditorNotes, s.Reviewers,
                ManuscriptUrl  = s.ManuscriptFilePath  != null ? _fileService.GetFileUrl(s.ManuscriptFilePath,  Request) : null,
                FiguresUrl     = s.FiguresFilePath     != null ? _fileService.GetFileUrl(s.FiguresFilePath,     Request) : null,
                CoverLetterUrl = s.CoverLetterFilePath != null ? _fileService.GetFileUrl(s.CoverLetterFilePath, Request) : null,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(submissions));
    }

    // ── GET /api/admin/submissions/{refId} ─────────────────────────────────
    [HttpGet("submissions/{refId}")]
    public async Task<IActionResult> GetOne(string refId)
    {
        var sub = await _db.Submissions.FirstOrDefaultAsync(s => s.RefId == refId);
        if (sub == null) return NotFound(ApiResponse.Fail("Not found"));
        return Ok(ApiResponse<Submission>.Ok(sub));
    }

    // ── PATCH /api/admin/submissions/status ────────────────────────────────
    [HttpPatch("submissions/status")]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusDto dto)
    {
        var sub = await _db.Submissions.FirstOrDefaultAsync(s => s.RefId == dto.RefId);
        if (sub == null) return NotFound(ApiResponse.Fail("Not found"));

        var allowedStatuses = new[]
        {
            "Under Editorial Review", "Initial Screening",
            "Revision Requested", "Accepted", "Rejected", "Published"
        };
        if (!allowedStatuses.Contains(dto.Status))
            return BadRequest(ApiResponse.Fail("Invalid status value"));

        sub.Status = dto.Status;
        await _db.SaveChangesAsync();

        _logger.LogInformation("Status updated: {RefId} → {Status}", dto.RefId, dto.Status);
        return Ok(ApiResponse.Ok("Status updated"));
    }

    // ── PATCH /api/admin/submissions/notes ─────────────────────────────────
    [HttpPatch("submissions/notes")]
    public async Task<IActionResult> UpdateNotes([FromBody] UpdateNotesDto dto)
    {
        var sub = await _db.Submissions.FirstOrDefaultAsync(s => s.RefId == dto.RefId);
        if (sub == null) return NotFound(ApiResponse.Fail("Not found"));

        sub.EditorNotes = _sanitizer.SanitizeLong(dto.Notes, 2000);
        if (dto.Reviewers != null)
            sub.Reviewers = _sanitizer.SanitizeLong(dto.Reviewers, 1000);

        await _db.SaveChangesAsync();
        return Ok(ApiResponse.Ok("Saved"));
    }

    // ── POST /api/admin/email ──────────────────────────────────────────────
    /// <summary>
    /// Send a custom email to an author from the admin panel.
    /// Rate limited to prevent accidental bulk sending.
    /// </summary>
    [HttpPost("email")]
    [EnableRateLimiting("admin_email")]
    public async Task<IActionResult> SendEmail([FromBody] AdminEmailDto dto)
    {
        if (string.IsNullOrEmpty(dto.To) || string.IsNullOrEmpty(dto.Subject) || string.IsNullOrEmpty(dto.Body))
            return BadRequest(ApiResponse.Fail("To, Subject, and Body are required."));

        // Verify the target email belongs to a real submission (prevents email abuse)
        var exists = await _db.Submissions.AnyAsync(s => s.Email == dto.To);
        if (!exists)
            return BadRequest(ApiResponse.Fail("Email address not found in submission records."));

        await _emailService.SendCustomEmailAsync(dto.To, dto.Subject, dto.Body);

        _logger.LogInformation("Admin email sent to {Email} re: {RefId}", dto.To, dto.RefId);
        return Ok(ApiResponse.Ok("Email sent"));
    }

    // ── GET /api/admin/stats ───────────────────────────────────────────────
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var stats = new
        {
            Total    = await _db.Submissions.CountAsync(),
            Review   = await _db.Submissions.CountAsync(s => s.Status == "Under Editorial Review"),
            Revision = await _db.Submissions.CountAsync(s => s.Status == "Revision Requested"),
            Accepted = await _db.Submissions.CountAsync(s => s.Status == "Accepted"),
            Rejected = await _db.Submissions.CountAsync(s => s.Status == "Rejected"),
            Published= await _db.Submissions.CountAsync(s => s.Status == "Published"),
        };
        return Ok(ApiResponse<object>.Ok(stats));
    }
}
