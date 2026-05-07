namespace JsbaiBackend.Services;
public interface IFileService {
    Task<string?> SaveBase64FileAsync(string? base64Data, string? fileName, string refId, string fileType);
    string GetFileUrl(string? filePath, HttpRequest request);
}
public class FileService : IFileService {
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<FileService> _logger;
    public FileService(IWebHostEnvironment env, ILogger<FileService> logger) { _env = env; _logger = logger; }
    public async Task<string?> SaveBase64FileAsync(string? base64Data, string? fileName, string refId, string fileType) {
        if (string.IsNullOrEmpty(base64Data) || string.IsNullOrEmpty(fileName)) return null;
        try {
            var folder = Path.Combine(_env.WebRootPath, "uploads", refId);
            Directory.CreateDirectory(folder);
            var safeName = $"{fileType}_{Path.GetFileName(fileName)}";
            var fullPath = Path.Combine(folder, safeName);
            await File.WriteAllBytesAsync(fullPath, Convert.FromBase64String(base64Data));
            return Path.Combine("uploads", refId, safeName);
        } catch (Exception ex) { _logger.LogError(ex, "File save failed"); return null; }
    }
    public string GetFileUrl(string? filePath, HttpRequest request) {
        if (string.IsNullOrEmpty(filePath)) return "";
        return $"{request.Scheme}://{request.Host}/{filePath.Replace("\\", "/")}";
    }
}
