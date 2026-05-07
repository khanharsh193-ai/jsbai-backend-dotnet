using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using JsbaiBackend.Data;
using JsbaiBackend.DTOs;
using JsbaiBackend.Models;
using JsbaiBackend.Services;

namespace JsbaiBackend.Controllers;

/// <summary>
/// Handles manuscript submissions with full security:
/// - Rate limiting: max 5 submissions per IP per hour
/// - File validation: magic byte checking
/// - Input sanitisation: strips XSS attacks
/// - Size limits enforced server-side
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SubmissionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IFileService _fileService;
    private readonly IFileValidationService _fileValidator;
    private readonly ISanitizationService _sanitizer;
    private readonly ILogger<SubmissionsController> _logger;

    public SubmissionsController(
        AppDbContext db,
        IEmailService emailService,
        IFileService fileService,
        IFileValidationService fileValidator,
        ISanitizationService sanitizer,
        ILogger<SubmissionsController> logger)
    {
        _db = db;
        _emailService = emailService;
        _fileService = fileService;
        _fileValidator = fileValidator;
        _sanitizer = sanitizer;
        _logger = logger;
    }

    // ── POST /api/submissions ──────────────────────────────────────────────
    /// <summary>
    /// Submit a manuscript.
    /// Rate limited: 5 requests per IP per hour.
    /// This prevents automated spam submissions.
    /// </summary>
    [HttpPost]
    [EnableRateLimiting("submissions")]   // ← applies the rate limit policy
    public async Task<IActionResult> Submit([FromBody] SubmissionDto dto)
    {
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed: " +
                string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage))));

        // ── File Validation ────────────────────────────────────────────────
        // Check manuscript file (required)
        if (string.IsNullOrEmpty(dto.ManuscriptFileBase64))
            return BadRequest(ApiResponse.Fail("Manuscript file is required."));

        var msError  = _fileValidator.GetRejectionReason(dto.ManuscriptFileBase64, dto.ManuscriptFileName, new[]{".pdf",".doc",".docx"});
        var figError = _fileValidator.GetRejectionReason(dto.FiguresFileBase64, dto.FiguresFileName, new[]{".jpg",".jpeg",".png",".tif",".tiff",".xlsx"});
        var clError  = _fileValidator.GetRejectionReason(dto.CoverLetterFileBase64, dto.CoverLetterFileName, new[]{".pdf",".doc",".docx"});

        if (msError  != null) return BadRequest(ApiResponse.Fail("Manuscript file: " + msError));
        if (figError != null) return BadRequest(ApiResponse.Fail("Figures file: " + figError));
        if (clError  != null) return BadRequest(ApiResponse.Fail("Cover letter: " + clError));

        // ── Input Sanitisation ─────────────────────────────────────────────
        // Strip any HTML/script tags from all text inputs before saving
        var refId = GenerateRefId();

        try
        {
            var msPath  = await _fileService.SaveBase64FileAsync(dto.ManuscriptFileBase64,  dto.ManuscriptFileName,  refId, "Manuscript");
            var figPath = await _fileService.SaveBase64FileAsync(dto.FiguresFileBase64,      dto.FiguresFileName,     refId, "Figures");
            var clPath  = await _fileService.SaveBase64FileAsync(dto.CoverLetterFileBase64,  dto.CoverLetterFileName, refId, "CoverLetter");

            var submission = new Submission
            {
                RefId               = refId,
                SubmittedAt         = DateTime.UtcNow,
                // ✅ All text fields sanitised before saving
                Title               = _sanitizer.SanitizeShort(dto.Title),
                ArticleType         = _sanitizer.SanitizeShort(dto.ArticleType),
                SubjectArea         = _sanitizer.SanitizeShort(dto.SubjectArea),
                Abstract            = _sanitizer.SanitizeLong(dto.Abstract, 3000),
                Keywords            = _sanitizer.SanitizeShort(dto.Keywords),
                FirstName           = _sanitizer.SanitizeShort(dto.FirstName, 100),
                LastName            = _sanitizer.SanitizeShort(dto.LastName, 100),
                Email               = _sanitizer.SanitizeShort(dto.Email, 200),
                Orcid               = _sanitizer.SanitizeShort(dto.Orcid, 25),
                Affiliation         = _sanitizer.SanitizeShort(dto.Affiliation, 400),
                CoAuthors           = _sanitizer.SanitizeLong(dto.CoAuthors, 2000),
                Funding             = _sanitizer.SanitizeShort(dto.Funding, 500),
                SuggestedReviewers  = _sanitizer.SanitizeLong(dto.SuggestedReviewers, 1000),
                ManuscriptFilePath  = msPath,
                FiguresFilePath     = figPath,
                CoverLetterFilePath = clPath,
                Status              = "Under Editorial Review",
            };

            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();

            var msUrl = msPath != null ? _fileService.GetFileUrl(msPath, Request) : null;

            // Send emails in background (don't slow down the response)
            _ = _emailService.SendAuthorConfirmationAsync(
                submission.Email,
                $"{submission.FirstName} {submission.LastName}",
                refId, submission.Title, submission.ArticleType, submission.Affiliation);

            _ = _emailService.SendEditorNotificationAsync(
                refId, submission.Title, submission.ArticleType,
                $"{submission.FirstName} {submission.LastName}",
                submission.Email, submission.Affiliation, msUrl);

            _logger.LogInformation("New submission: {RefId} from IP: {IP}",
                refId, HttpContext.Connection.RemoteIpAddress);

            return StatusCode(201, ApiResponse<object>.Ok(new { refId }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing submission {RefId}", refId);
            return StatusCode(500, ApiResponse.Fail("An error occurred. Please try again."));
        }
    }

    // ── GET /api/submissions/track ─────────────────────────────────────────
    /// <summary>
    /// Allows authors to track their submission by RefId + Email.
    /// Rate limited: 10 requests per IP per hour.
    /// Email verification ensures only the submitting author can see status.
    /// </summary>
    [HttpGet("track")]
    [EnableRateLimiting("tracking")]
    public async Task<IActionResult> Track([FromQuery] string refId, [FromQuery] string email)
    {
        if (string.IsNullOrEmpty(refId) || string.IsNullOrEmpty(email))
            return BadRequest(ApiResponse.Fail("Reference ID and email are required."));

        // Sanitise inputs
        refId = _sanitizer.SanitizeShort(refId.ToUpperInvariant(), 20);
        email = _sanitizer.SanitizeShort(email.ToLowerInvariant(), 200);

        var submission = await _db.Submissions
            .Where(s => s.RefId == refId && s.Email.ToLower() == email)
            .Select(s => new
            {
                s.RefId,
                s.SubmittedAt,
                s.Title,
                s.ArticleType,
                s.SubjectArea,
                s.Affiliation,
                s.Status,
                // Don't return sensitive data like file paths or notes
            })
            .FirstOrDefaultAsync();

        if (submission == null)
            // Generic error — don't reveal if refId exists (prevents enumeration)
            return NotFound(ApiResponse.Fail("Submission not found. Please check your Reference ID and email address."));

        return Ok(ApiResponse<object>.Ok(submission));
    }

    // ── GET /api/submissions/health ────────────────────────────────────────
    [HttpGet("health")]
    public IActionResult Health() => Ok(ApiResponse.Ok("JSBAI API is running"));

    private static string GenerateRefId()
    {
        var year = DateTime.UtcNow.Year;
        var rnd  = Random.Shared.Next(1000, 9999);
        return $"JSBAI-{year}-{rnd}";
    }
}
