using LangArt.Api.Features.Uploads.Dtos;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Uploads;

[ApiController]
[Route("api/uploads")]
[Authorize(Roles = "admin,teacher")]
public class UploadsController : ControllerBase
{
    private readonly UploadsService _uploads;

    public UploadsController(UploadsService uploads)
    {
        _uploads = uploads;
    }

    [HttpPost("resource")]
    [RequestSizeLimit(50 * 1024 * 1024)]
    public Task<UploadResponse> UploadResource(IFormFile file) =>
        _uploads.SaveAsync(file, "resources");

    [HttpPost("thumbnail")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public Task<UploadResponse> UploadThumbnail(IFormFile file) =>
        _uploads.SaveAsync(file, "thumbnails");

    [HttpDelete("file")]
    public IActionResult DeleteFile([FromQuery] string url)
    {
        _uploads.DeleteFile(url);
        return Ok(new { });
    }
}

/// <summary>
/// Student-accessible audio upload for Speaking exercises. Lives in its own
/// controller so it doesn't inherit the admin/teacher role gate from
/// <see cref="UploadsController"/>. Any authenticated user (including students)
/// can POST here; files land in <c>uploads/speaking/</c>.
/// </summary>
[ApiController]
[Route("api/uploads")]
[Authorize]
public class AudioUploadsController : ControllerBase
{
    private readonly UploadsService _uploads;

    public AudioUploadsController(UploadsService uploads)
    {
        _uploads = uploads;
    }

    [HttpPost("audio")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public Task<UploadResponse> UploadAudio(IFormFile file) =>
        _uploads.SaveAsync(file, "speaking");
}
