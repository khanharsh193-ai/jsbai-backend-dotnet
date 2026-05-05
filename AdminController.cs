using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using JsbaiBackend.Data;
using JsbaiBackend.DTOs;
using JsbaiBackend.Models;
using JsbaiBackend.Services;

namespace JsbaiBackend.Controllers;

/// <summary>
/// This is the CONTROLLER for the admin dashboard.
///
/// All endpoints here require the admin password in the request header.
/// If the password is wrong, the request is rejected immediately.
///
/// Route: /api/admin
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IFileService _fileService;
    private readonly IConfiguration _config;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        AppDbContext db,
        IFileService fileService,
        IConfiguration config,
        ILogger<AdminController> logger)
    {
        _db = db;
        _fileService = fileService;
        _config = config;
        _logger = logger;
    }

    // ── Password check helper ──────────────────────────────────────────────
    /// <summary>
    /// Checks the X-Admin-Password header on every admin request.
    /// Like a bouncer checking your ID at the door.
    /// </summary>
    private bool IsAuthorized()
    {
        var headerPwd = Request.Headers["X-Admin-Password"].FirstOrDefault();
        var configPwd = _config["AdminPassword"];
        return headerPwd == configPwd && !string.IsNullOrEmpty(configPwd);
    }

    // ── GET /api/admin/submissions ─────────────────────────────────────────
    /// <summary>
    /// Returns all submissions for the admin dashboard table.
    /// </summary>
    [HttpGet("submissions")]
    public async Task<IActionResult> GetAll()
    {
        if (!IsAuthorized())
            return Unauthorized(ApiResponse.Fail("Unauthorized"));

        // EF Core translates this to: SELECT * FROM Submissions ORDER BY SubmittedAt DESC
        var submissions = await _db.Submissions
            .OrderByDescending(s => s.SubmittedAt)
            .Select(s => new
            {
                s.Id,
                s.RefId,
                s.SubmittedAt,
                s.Title,
                s.ArticleType,
                s.SubjectArea,
                s.Keywords,
                s.FirstName,
                s.LastName,
                s.Email,
                s.Orcid,
                s.Affiliation,
                s.CoAuthors,
                s.Funding,
                s.SuggestedReviewers,
                s.Status,
                s.EditorNotes,
                // Build full download URLs for each file
                ManuscriptUrl    = s.ManuscriptFilePath  != null ? _fileService.GetFileUrl(s.ManuscriptFilePath,  Request) : null,
                FiguresUrl       = s.FiguresFilePath     != null ? _fileService.GetFileUrl(s.FiguresFilePath,     Request) : null,
                CoverLetterUrl   = s.CoverLetterFilePath != null ? _fileService.GetFileUrl(s.CoverLetterFilePath, Request) : null,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(submissions));
    }

    // ── GET /api/admin/submissions/{refId} ─────────────────────────────────
    /// <summary>
    /// Returns a single submission by its reference ID.
    /// </summary>
    [HttpGet("submissions/{refId}")]
    public async Task<IActionResult> GetOne(string refId)
    {
        if (!IsAuthorized())
            return Unauthorized(ApiResponse.Fail("Unauthorized"));

        // EF Core: SELECT * FROM Submissions WHERE RefId = @refId
        var submission = await _db.Submissions
            .FirstOrDefaultAsync(s => s.RefId == refId);

        if (submission == null)
            return NotFound(ApiResponse.Fail($"Submission {refId} not found"));

        return Ok(ApiResponse<Submission>.Ok(submission));
    }

    // ── PATCH /api/admin/submissions/status ────────────────────────────────
    /// <summary>
    /// Updates the status of a submission (e.g. Under Review → Accepted).
    /// Called when editor changes the dropdown in the admin panel.
    /// </summary>
    [HttpPatch("submissions/status")]
    public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusDto dto)
    {
        if (!IsAuthorized())
            return Unauthorized(ApiResponse.Fail("Unauthorized"));

        var submission = await _db.Submissions
            .FirstOrDefaultAsync(s => s.RefId == dto.RefId);

        if (submission == null)
            return NotFound(ApiResponse.Fail($"Submission {dto.RefId} not found"));

        submission.Status = dto.Status;

        // EF Core: UPDATE Submissions SET Status = @status WHERE RefId = @refId
        await _db.SaveChangesAsync();

        _logger.LogInformation("Status updated: {RefId} → {Status}", dto.RefId, dto.Status);
        return Ok(ApiResponse.Ok($"Status updated to: {dto.Status}"));
    }

    // ── PATCH /api/admin/submissions/notes ─────────────────────────────────
    /// <summary>
    /// Saves editor's private notes on a submission.
    /// </summary>
    [HttpPatch("submissions/notes")]
    public async Task<IActionResult> UpdateNotes([FromBody] UpdateNotesDto dto)
    {
        if (!IsAuthorized())
            return Unauthorized(ApiResponse.Fail("Unauthorized"));

        var submission = await _db.Submissions
            .FirstOrDefaultAsync(s => s.RefId == dto.RefId);

        if (submission == null)
            return NotFound(ApiResponse.Fail($"Submission {dto.RefId} not found"));

        submission.EditorNotes = dto.Notes;
        await _db.SaveChangesAsync();

        return Ok(ApiResponse.Ok("Notes saved"));
    }

    // ── GET /api/admin/stats ───────────────────────────────────────────────
    /// <summary>
    /// Returns counts for the stats cards at the top of the admin dashboard.
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        if (!IsAuthorized())
            return Unauthorized(ApiResponse.Fail("Unauthorized"));

        var stats = new
        {
            Total    = await _db.Submissions.CountAsync(),
            Review   = await _db.Submissions.CountAsync(s => s.Status == "Under Editorial Review"),
            Revision = await _db.Submissions.CountAsync(s => s.Status == "Revision Requested"),
            Accepted = await _db.Submissions.CountAsync(s => s.Status == "Accepted"),
            Rejected = await _db.Submissions.CountAsync(s => s.Status == "Rejected"),
        };

        return Ok(ApiResponse<object>.Ok(stats));
    }
}
