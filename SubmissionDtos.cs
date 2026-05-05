using System.ComponentModel.DataAnnotations;

namespace JsbaiBackend.DTOs;

/// <summary>
/// DTO = Data Transfer Object.
/// This defines exactly what data the frontend must send when submitting a manuscript.
/// The [Required] tags automatically validate the form — if a required field is missing,
/// the backend rejects it before even running our code.
/// </summary>
public class SubmissionDto
{
    // ── Manuscript details ────────────────────────────────────────────────
    [Required(ErrorMessage = "Manuscript title is required")]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Article type is required")]
    public string ArticleType { get; set; } = string.Empty;

    [Required(ErrorMessage = "Subject area is required")]
    public string SubjectArea { get; set; } = string.Empty;

    [Required(ErrorMessage = "Abstract is required")]
    [MaxLength(3000)]
    public string Abstract { get; set; } = string.Empty;

    [Required(ErrorMessage = "Keywords are required")]
    public string Keywords { get; set; } = string.Empty;

    // ── Author details ────────────────────────────────────────────────────
    [Required(ErrorMessage = "First name is required")]
    public string FirstName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Last name is required")]
    public string LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email address is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public string Email { get; set; } = string.Empty;

    public string? Orcid { get; set; }

    [Required(ErrorMessage = "Affiliation is required")]
    public string Affiliation { get; set; } = string.Empty;

    public string? CoAuthors { get; set; }

    // ── Administrative ────────────────────────────────────────────────────
    public string? Funding { get; set; }
    public string? SuggestedReviewers { get; set; }

    // ── Files (sent as Base64 encoded strings from the frontend) ──────────
    // Base64 means the file is converted to a long text string for sending over HTTP
    public string? ManuscriptFileBase64 { get; set; }
    public string? ManuscriptFileName { get; set; }

    public string? FiguresFileBase64 { get; set; }
    public string? FiguresFileName { get; set; }

    public string? CoverLetterFileBase64 { get; set; }
    public string? CoverLetterFileName { get; set; }
}

/// <summary>
/// DTO for updating the status of a submission from the admin panel.
/// </summary>
public class UpdateStatusDto
{
    [Required]
    public string RefId { get; set; } = string.Empty;

    [Required]
    public string Status { get; set; } = string.Empty;
}

/// <summary>
/// DTO for saving editor notes.
/// </summary>
public class UpdateNotesDto
{
    [Required]
    public string RefId { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;
}
