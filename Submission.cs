namespace JsbaiBackend.Models;

/// <summary>
/// This is the MODEL in MVC.
/// It represents one manuscript submission — exactly one row in the database.
/// Every property here becomes a column in the database table.
/// </summary>
public class Submission
{
    // Primary key — SQLite auto-generates this number for each new row
    public int Id { get; set; }

    // Auto-generated reference ID like JSBAI-2025-4821
    public string RefId { get; set; } = string.Empty;

    // When the form was submitted
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // ── Manuscript details ────────────────────────────────────────────────
    public string Title { get; set; } = string.Empty;
    public string ArticleType { get; set; } = string.Empty;
    public string SubjectArea { get; set; } = string.Empty;
    public string Abstract { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;

    // ── Author details ────────────────────────────────────────────────────
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Orcid { get; set; }
    public string Affiliation { get; set; } = string.Empty;
    public string? CoAuthors { get; set; }

    // ── Administrative ────────────────────────────────────────────────────
    public string? Funding { get; set; }
    public string? SuggestedReviewers { get; set; }

    // ── Uploaded file paths (stored on server disk) ───────────────────────
    public string? ManuscriptFilePath { get; set; }
    public string? FiguresFilePath { get; set; }
    public string? CoverLetterFilePath { get; set; }

    // ── Editorial workflow ────────────────────────────────────────────────
    public string Status { get; set; } = "Under Editorial Review";
    public string? EditorNotes { get; set; }
}
