namespace JsbaiBackend.Models;
public class Submission {
    public int Id { get; set; }
    public string RefId { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
    public string Title { get; set; } = string.Empty;
    public string ArticleType { get; set; } = string.Empty;
    public string SubjectArea { get; set; } = string.Empty;
    public string Abstract { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Orcid { get; set; }
    public string Affiliation { get; set; } = string.Empty;
    public string? CoAuthors { get; set; }
    public string? Funding { get; set; }
    public string? SuggestedReviewers { get; set; }
    public string? ManuscriptFilePath { get; set; }
    public string? FiguresFilePath { get; set; }
    public string? CoverLetterFilePath { get; set; }
    public string Status { get; set; } = "Under Editorial Review";
    public string? EditorNotes { get; set; }
    public string? Reviewers { get; set; }
}
