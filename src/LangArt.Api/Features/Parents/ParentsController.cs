using LangArt.Api.Common.Auth;
using LangArt.Api.Features.Parents.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Parents;

[ApiController]
[Route("api/parents")]
[Authorize]
public class ParentsController : ControllerBase
{
    private readonly ParentsService _svc;
    private readonly ICurrentUser _currentUser;

    public ParentsController(ParentsService svc, ICurrentUser currentUser)
    {
        _svc = svc;
        _currentUser = currentUser;
    }

    /// <summary>Children linked to the currently logged-in parent.</summary>
    [HttpGet("my/children")]
    [Authorize(Roles = "parent")]
    public Task<IReadOnlyList<ChildSummaryDto>> MyChildren(CancellationToken ct) =>
        _svc.GetMyChildrenAsync(_currentUser.Id, ct);

    /// <summary>Admin-only: create a new parent account and link it to a child.</summary>
    [HttpPost("create")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> CreateParent([FromBody] CreateParentAccountRequest req, CancellationToken ct)
    {
        var parent = await _svc.CreateParentAccountAsync(req, ct);
        return Ok(new { id = parent.Id, email = parent.Email, full_name = parent.FullName });
    }

    /// <summary>Admin-only: link an existing parent account to another child.</summary>
    [HttpPost("{parentId:guid}/children")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> LinkChild(Guid parentId, [FromBody] LinkChildRequest req, CancellationToken ct)
    {
        await _svc.LinkChildAsync(parentId, req, ct);
        return Ok(new { });
    }

    [HttpDelete("{parentId:guid}/children/{childId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> UnlinkChild(Guid parentId, Guid childId, CancellationToken ct)
    {
        await _svc.UnlinkChildAsync(parentId, childId, ct);
        return NoContent();
    }
}
