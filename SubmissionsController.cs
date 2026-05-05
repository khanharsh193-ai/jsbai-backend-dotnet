using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JsbaiBackend.Data;
using JsbaiBackend.DTOs;
using JsbaiBackend.Models;
using JsbaiBackend.Services;

namespace JsbaiBackend.Controllers;

/// <summary>
/// This is the CONTROLLER in MVC for manuscript submissions.
///
/// A Controller is like a traffic officer — it:
/// 1. Receives incoming requests from the frontend
/// 2. Validates the data
/// 3. Tells the service/database what to do
/// 4. Sends a response back
///
/// Route: /api/submissions
/// All endpoints in this controller start with /api/submissions/...
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class SubmissionsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IEmailService _emailService;
    private readonly IFileService _fileService;
    private readonly ILogger<SubmissionsController> _logger;

    // Constructor Injection — .NET automatically provides these services
    // This is called Dependency Injection (DI) — a core .NET concept
    public SubmissionsController(
        AppDbContext db,
        IEmailService emailService,
        IFileService fileService,
        ILogger<SubmissionsController> logger)
    {
        _db = db;
        _emailService = emailService;
        _fileService = fileService;
        _logger = logger;
    }

    // ── POST /api/submissions ──────────────────────────────────────────────
    /// <summary>
    /// Receives a new manuscript submission from the journal website form.
    /// This is called when an author clicks "Submit Manuscript".
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Submit([FromBody] SubmissionDto dto)
    {
        // If required fields are missing, return 400 Bad Request automatically
        if (!ModelState.IsValid)
            return BadRequest(ApiResponse.Fail("Validation failed: " +
                string.Join(", ", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage))));

        try
        {
            // Generate a unique reference ID like JSBAI-2025-4821
            var refId = GenerateRefId();

            // Save uploaded files to disk
            var msPath  = await _fileService.SaveBase64FileAsync(
                dto.ManuscriptFileBase64, dto.ManuscriptFileName, refId, "Manuscript");
            var figPath = await _fileService.SaveBase64FileAsync(
                dto.FiguresFileBase64, dto.FiguresFileName, refId, "Figures");
            var clPath  = await _fileService.SaveBase64FileAsync(
                dto.CoverLetterFileBase64, dto.CoverLetterFileName, refId, "CoverLetter");

            // Create a new Submission object (MODEL) and fill it with the form data
            var submission = new Submission
            {
                RefId               = refId,
                SubmittedAt         = DateTime.UtcNow,
                Title               = dto.Title,
                ArticleType         = dto.ArticleType,
                SubjectArea         = dto.SubjectArea,
                Abstract            = dto.Abstract,
                Keywords            = dto.Keywords,
                FirstName           = dto.FirstName,
                LastName            = dto.LastName,
                Email               = dto.Email,
                Orcid               = dto.Orcid,
                Affiliation         = dto.Affiliation,
                CoAuthors           = dto.CoAuthors,
                Funding             = dto.Funding,
                SuggestedReviewers  = dto.SuggestedReviewers,
                ManuscriptFilePath  = msPath,
                FiguresFilePath     = figPath,
                CoverLetterFilePath = clPath,
                Status              = "Under Editorial Review"
            };

            // Save to database — EF Core translates this to SQL INSERT
            _db.Submissions.Add(submission);
            await _db.SaveChangesAsync();

            // Build file URL for editor notification email
            var msUrl = msPath != null ? _fileService.GetFileUrl(msPath, Request) : null;

            // Send confirmation email to author (runs in background — doesn't slow down response)
            _ = _emailService.SendAuthorConfirmationAsync(
                dto.Email,
                $"{dto.FirstName} {dto.LastName}",
                refId, dto.Title, dto.ArticleType, dto.Affiliation);

            // Notify editor
            _ = _emailService.SendEditorNotificationAsync(
                refId, dto.Title, dto.ArticleType,
                $"{dto.FirstName} {dto.LastName}",
                dto.Email, dto.Affiliation, msUrl);

            _logger.LogInformation("New submission received: {RefId} — {Title}", refId, dto.Title);

            // Return 201 Created with the reference ID
            return StatusCode(201, ApiResponse<object>.Ok(new { refId }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing submission");
            return StatusCode(500, ApiResponse.Fail("An error occurred processing your submission. Please try again."));
        }
    }

    // ── GET /api/submissions/health ────────────────────────────────────────
    /// <summary>
    /// Health check — lets us verify the API is running.
    /// </summary>
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(ApiResponse.Ok("JSBAI API is running"));

    // ── Helper ─────────────────────────────────────────────────────────────
    private static string GenerateRefId()
    {
        var year = DateTime.UtcNow.Year;
        var rnd  = Random.Shared.Next(1000, 9999);
        return $"JSBAI-{year}-{rnd}";
    }
}
