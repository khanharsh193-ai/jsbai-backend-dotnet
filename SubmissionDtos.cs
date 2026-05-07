using System.ComponentModel.DataAnnotations;
namespace JsbaiBackend.DTOs;
public class SubmissionDto {
    [Required][MaxLength(500)] public string Title { get; set; } = string.Empty;
    [Required] public string ArticleType { get; set; } = string.Empty;
    [Required] public string SubjectArea { get; set; } = string.Empty;
    [Required][MaxLength(3000)] public string Abstract { get; set; } = string.Empty;
    [Required] public string Keywords { get; set; } = string.Empty;
    [Required] public string FirstName { get; set; } = string.Empty;
    [Required] public string LastName { get; set; } = string.Empty;
    [Required][EmailAddress] public string Email { get; set; } = string.Empty;
    public string? Orcid { get; set; }
    [Required] public string Affiliation { get; set; } = string.Empty;
    public string? CoAuthors { get; set; }
    public string? Funding { get; set; }
    public string? SuggestedReviewers { get; set; }
    public string? ManuscriptFileBase64 { get; set; }
    public string? ManuscriptFileName { get; set; }
    public string? FiguresFileBase64 { get; set; }
    public string? FiguresFileName { get; set; }
    public string? CoverLetterFileBase64 { get; set; }
    public string? CoverLetterFileName { get; set; }
}
public class UpdateStatusDto {
    [Required] public string RefId { get; set; } = string.Empty;
    [Required] public string Status { get; set; } = string.Empty;
}
public class UpdateNotesDto {
    [Required] public string RefId { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public string? Reviewers { get; set; }
}
public class AdminEmailDto {
    public string RefId { get; set; } = string.Empty;
    [Required] public string To { get; set; } = string.Empty;
    [Required] public string Subject { get; set; } = string.Empty;
    [Required] public string Body { get; set; } = string.Empty;
}
