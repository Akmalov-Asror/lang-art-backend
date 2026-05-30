using LangArt.Api.Features.Branches.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Branches;

[ApiController]
[Route("api/branches")]
[Authorize(Roles = "admin,teacher")]
public class BranchesController(BranchesService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<BranchDto>>> List([FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await service.ListAsync(includeInactive, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchDto>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<BranchDto>> Create([FromBody] CreateBranchRequest req, CancellationToken ct)
        => Ok(await service.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<BranchDto>> Update(Guid id, [FromBody] UpdateBranchRequest req, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(new { success = true });
    }

    [HttpPost("assign/profile/{profileId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> AssignProfile(Guid profileId, [FromBody] AssignBranchRequest req, CancellationToken ct)
    {
        await service.AssignProfileAsync(profileId, req.BranchId, ct);
        return Ok(new { success = true });
    }

    [HttpPost("assign/group/{groupId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> AssignGroup(Guid groupId, [FromBody] AssignBranchRequest req, CancellationToken ct)
    {
        await service.AssignGroupAsync(groupId, req.BranchId, ct);
        return Ok(new { success = true });
    }
}
