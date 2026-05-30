using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Uploads.Dtos;

public class UploadResponse
{
    public string Url { get; set; } = string.Empty;     // server-relative URL, e.g. /uploads/resources/abc.pdf
    public string FileName { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty; // extension without leading dot
    public long FileSize { get; set; }
}

public class DeleteFileRequest
{
    [Required]
    public string Url { get; set; } = string.Empty;
}
