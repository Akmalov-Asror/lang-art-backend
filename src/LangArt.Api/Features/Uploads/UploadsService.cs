using LangArt.Api.Common.Configuration;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Features.Uploads.Dtos;
using Microsoft.Extensions.Options;

namespace LangArt.Api.Features.Uploads;

public class UploadsService
{
    private readonly UploadsOptions _opt;
    private readonly HashSet<string> _allowedTypes;
    private readonly ILogger<UploadsService> _logger;

    public UploadsService(IOptions<UploadsOptions> options, ILogger<UploadsService> logger)
    {
        _opt = options.Value;
        _logger = logger;
        _allowedTypes = _opt.AllowedTypes
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToHashSet();
    }

    public async Task<UploadResponse> SaveAsync(IFormFile file, string subdir)
    {
        if (file is null || file.Length == 0)
            throw new BadRequestException("No file uploaded");
        if (file.Length > _opt.MaxBytes)
            throw new BadRequestException($"File exceeds size limit of {_opt.MaxBytes} bytes");

        var ext = (Path.GetExtension(file.FileName)?.TrimStart('.') ?? string.Empty).ToLowerInvariant();
        if (ext.Length == 0) throw new BadRequestException("File has no extension");
        if (!_allowedTypes.Contains(ext))
            throw new BadRequestException($"File type '{ext}' is not allowed");

        var safeName = $"{Guid.NewGuid():N}.{ext}";
        var targetDir = Path.Combine(Path.GetFullPath(_opt.Dir), subdir);
        Directory.CreateDirectory(targetDir);
        var fullPath = Path.Combine(targetDir, safeName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var url = $"/uploads/{subdir}/{safeName}";
        return new UploadResponse
        {
            Url = url,
            FileName = safeName,
            FileType = ext,
            FileSize = file.Length,
        };
    }

    public void DeleteFile(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) throw new BadRequestException("Missing url");
        if (!url.StartsWith("/uploads/", StringComparison.Ordinal))
            throw new BadRequestException("URL is not under /uploads/");

        var relative = url["/uploads/".Length..].Replace('\\', '/');
        // Reject path-traversal attempts.
        if (relative.Contains("..", StringComparison.Ordinal))
            throw new BadRequestException("Invalid path");

        var fullPath = Path.GetFullPath(Path.Combine(Path.GetFullPath(_opt.Dir), relative));
        var baseDir = Path.GetFullPath(_opt.Dir);
        if (!fullPath.StartsWith(baseDir, StringComparison.OrdinalIgnoreCase))
            throw new BadRequestException("Refusing to delete outside uploads dir");

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }
        else
        {
            _logger.LogWarning("Attempted to delete non-existent upload {Path}", fullPath);
        }
    }
}
