namespace JsbaiBackend.Services;

public interface IFileService
{
    Task<string?> SaveBase64FileAsync(string? base64Data, string? fileName, string refId, string fileType);
    string GetFileUrl(string? filePath, HttpRequest request);
}

/// <summary>
/// Handles saving uploaded files to the server's disk.
/// 
/// When an author uploads a manuscript:
/// 1. The browser converts it to Base64 (a text representation of the file)
/// 2. The text is sent to this server
/// 3. This service converts it back to a real file and saves it
/// 
/// Files are saved in: /uploads/{refId}/
/// </summary>
public class FileService : IFileService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileService> _logger;

    public FileService(IWebHostEnvironment env, ILogger<FileService> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<string?> SaveBase64FileAsync(string? base64Data, string? fileName,
        string refId, string fileType)
    {
        if (string.IsNullOrEmpty(base64Data) || string.IsNullOrEmpty(fileName))
            return null;

        try
        {
            // Create the uploads folder if it doesn't exist
            // Path: wwwroot/uploads/JSBAI-2025-1234/
            var uploadFolder = Path.Combine(_env.WebRootPath, "uploads", refId);
            Directory.CreateDirectory(uploadFolder);

            // Build a safe file name — e.g. "Manuscript_originalname.pdf"
            var safeFileName = $"{fileType}_{Path.GetFileName(fileName)}";
            var fullPath = Path.Combine(uploadFolder, safeFileName);

            // Convert Base64 text back to raw bytes and write to disk
            var fileBytes = Convert.FromBase64String(base64Data);
            await File.WriteAllBytesAsync(fullPath, fileBytes);

            // Return the relative path (used to build a URL later)
            return Path.Combine("uploads", refId, safeFileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save file {FileName} for {RefId}", fileName, refId);
            return null;
        }
    }

    public string GetFileUrl(string? filePath, HttpRequest request)
    {
        if (string.IsNullOrEmpty(filePath)) return "";
        // Build a full URL like: https://yourapp.railway.app/uploads/JSBAI-2025-1234/Manuscript_file.pdf
        return $"{request.Scheme}://{request.Host}/{filePath.Replace("\\", "/")}";
    }
}
