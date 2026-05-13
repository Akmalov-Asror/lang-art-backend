using LangArt.Api.Common.Auth;
using LangArt.Api.Common.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Certificates;

[ApiController]
[Route("api/certificates")]
[Authorize]
public class CertificatesController : ControllerBase
{
    private readonly CertificateService _svc;
    private readonly ICurrentUser _currentUser;

    public CertificatesController(CertificateService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Downloads the certificate as a PDF. Students may only fetch their own; admins/teachers
    /// may pass <c>?userId=…</c> to fetch on behalf of any user.
    /// </summary>
    [HttpGet("courses/{courseId:guid}")]
    [Produces("application/pdf")]
    public async Task<IActionResult> Download(Guid courseId, [FromQuery] Guid? userId)
    {
        var targetUserId = userId ?? _currentUser.Id;
        if (targetUserId != _currentUser.Id && _currentUser.Role == "student")
        {
            throw new ForbiddenException("Cannot download another student's certificate");
        }

        var (pdf, fileName) = await _svc.GenerateAsync(targetUserId, courseId);
        // Return raw PDF — this bypasses the ApiResponseFilter envelope, which is what we want for a file download.
        return File(pdf, "application/pdf", fileName);
    }
}
