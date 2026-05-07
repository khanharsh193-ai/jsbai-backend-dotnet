namespace JsbaiBackend.Services;

/// <summary>
/// Validates uploaded files to ensure they are actually what they claim to be.
///
/// WHY THIS MATTERS:
/// Without validation, someone could rename a virus.exe to manuscript.pdf
/// and upload it. This service checks the actual file contents (magic bytes),
/// not just the filename extension.
///
/// MAGIC BYTES explained:
/// Every file type starts with specific bytes that identify what it is.
/// For example, all PDF files start with the bytes: 25 50 44 46 (%PDF)
/// We check these bytes to verify the file is genuine.
/// </summary>
public interface IFileValidationService
{
    bool IsValidManuscript(string? base64Data, string? fileName);
    bool IsValidImage(string? base64Data, string? fileName);
    string? GetRejectionReason(string? base64Data, string? fileName, string[] allowedExtensions);
}

public class FileValidationService : IFileValidationService
{
    // Maximum file sizes
    private const int MaxManuscriptBytes = 15 * 1024 * 1024; // 15 MB
    private const int MaxImageBytes      = 15 * 1024 * 1024; // 15 MB
    private const int MaxCoverLetterBytes = 5 * 1024 * 1024;  // 5 MB

    // Allowed extensions for manuscripts
    private static readonly string[] ManuscriptExtensions = { ".pdf", ".doc", ".docx" };
    private static readonly string[] ImageExtensions      = { ".jpg", ".jpeg", ".png", ".tif", ".tiff", ".xlsx" };

    // Magic bytes — the actual bytes that start each file type
    private static readonly Dictionary<string, byte[]> MagicBytes = new()
    {
        { "pdf",  new byte[] { 0x25, 0x50, 0x44, 0x46 } },                         // %PDF
        { "docx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },                         // PK (ZIP format, which docx uses)
        { "doc",  new byte[] { 0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1 } },// OLE2 format
        { "jpg",  new byte[] { 0xFF, 0xD8, 0xFF } },                                // JPEG
        { "png",  new byte[] { 0x89, 0x50, 0x4E, 0x47 } },                         // PNG
        { "tif",  new byte[] { 0x49, 0x49, 0x2A, 0x00 } },                         // TIFF (little-endian)
        { "xlsx", new byte[] { 0x50, 0x4B, 0x03, 0x04 } },                         // Same as docx (ZIP)
    };

    public bool IsValidManuscript(string? base64Data, string? fileName)
        => GetRejectionReason(base64Data, fileName, ManuscriptExtensions) == null;

    public bool IsValidImage(string? base64Data, string? fileName)
        => GetRejectionReason(base64Data, fileName, ImageExtensions) == null;

    public string? GetRejectionReason(string? base64Data, string? fileName, string[] allowedExtensions)
    {
        if (string.IsNullOrEmpty(base64Data) || string.IsNullOrEmpty(fileName))
            return null; // No file — that's OK (optional fields)

        // ── Check 1: File extension ──────────────────────────────────────
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
            return $"File type '{ext}' is not allowed. Accepted: {string.Join(", ", allowedExtensions)}";

        // ── Check 2: Decode and check file size ──────────────────────────
        byte[] fileBytes;
        try
        {
            fileBytes = Convert.FromBase64String(base64Data);
        }
        catch
        {
            return "Invalid file data — could not decode the file.";
        }

        var maxSize = ext is ".pdf" or ".doc" or ".docx" ? MaxManuscriptBytes : MaxImageBytes;
        if (fileBytes.Length > maxSize)
            return $"File size ({fileBytes.Length / 1024 / 1024}MB) exceeds maximum allowed ({maxSize / 1024 / 1024}MB)";

        if (fileBytes.Length < 8)
            return "File is too small to be a valid document.";

        // ── Check 3: Magic bytes verification ────────────────────────────
        var extKey = ext.TrimStart('.');
        if (extKey == "jpeg") extKey = "jpg";

        if (MagicBytes.TryGetValue(extKey, out var magic))
        {
            for (int i = 0; i < magic.Length; i++)
            {
                if (i >= fileBytes.Length || fileBytes[i] != magic[i])
                    return $"File content does not match the declared type '{ext}'. Possible file tampering detected.";
            }
        }

        // ── Check 4: No executable content ───────────────────────────────
        // Block files that look like executables regardless of extension
        var exeMagic = new byte[] { 0x4D, 0x5A }; // MZ header (Windows executables)
        if (fileBytes.Length >= 2 && fileBytes[0] == exeMagic[0] && fileBytes[1] == exeMagic[1])
            return "File appears to be an executable — this file type is not permitted.";

        return null; // All checks passed
    }
}
