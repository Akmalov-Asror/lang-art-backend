using LangArt.Api.Features.Crm.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Crm;

[ApiController]
[Route("api/crm/leads")]
[Authorize(Roles = "admin,teacher")]
public class CrmController(CrmService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LeadDto>>> List([FromQuery] string? stage, [FromQuery] Guid? assignedTo, CancellationToken ct)
        => Ok(await service.ListAsync(stage, assignedTo, ct));

    [HttpGet("stats")]
    public async Task<ActionResult<CrmStatsDto>> Stats(CancellationToken ct)
        => Ok(await service.GetStatsAsync(ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LeadDto>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    [HttpGet("{id:guid}/activities")]
    public async Task<ActionResult<List<LeadActivityDto>>> Activities(Guid id, CancellationToken ct)
        => Ok(await service.ListActivitiesAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<LeadDto>> Create([FromBody] CreateLeadRequest req, CancellationToken ct)
        => Ok(await service.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<LeadDto>> Update(Guid id, [FromBody] UpdateLeadRequest req, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, req, ct));

    [HttpPost("{id:guid}/stage")]
    public async Task<ActionResult<LeadDto>> ChangeStage(Guid id, [FromBody] ChangeStageRequest req, CancellationToken ct)
        => Ok(await service.ChangeStageAsync(id, req, ct));

    [HttpPost("{id:guid}/activities")]
    public async Task<ActionResult<LeadActivityDto>> AddActivity(Guid id, [FromBody] AddActivityRequest req, CancellationToken ct)
        => Ok(await service.AddActivityAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(new { success = true });
    }
}
