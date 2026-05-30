using LangArt.Api.Features.Referrals.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Referrals;

[ApiController]
[Route("api/referrals")]
[Authorize]
public class ReferralsController(ReferralsService service) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<MyReferralStatusDto>> Me(CancellationToken ct)
        => Ok(await service.GetMyStatusAsync(ct));

    [HttpGet("alumni")]
    public async Task<ActionResult<List<AlumniDto>>> Alumni(CancellationToken ct)
        => Ok(await service.ListAlumniAsync(ct));

    [HttpPost("leads/{leadId:guid}/attach")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Attach(Guid leadId, [FromBody] AttachReferralRequest req, CancellationToken ct)
    {
        await service.AttachToLeadAsync(leadId, req, ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/reward")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Reward(Guid id, [FromBody] RewardReferralRequest req, CancellationToken ct)
    {
        await service.RewardAsync(id, req, ct);
        return Ok(new { success = true });
    }

    [HttpPost("profiles/{profileId:guid}/alumni")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> SetAlumni(Guid profileId, [FromBody] SetAlumniRequest req, CancellationToken ct)
    {
        await service.SetAlumniAsync(profileId, req, ct);
        return Ok(new { success = true });
    }
}
