using LangArt.Api.Features.Clubs.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Clubs;

[ApiController]
[Route("api/clubs/events")]
[Authorize]
public class ClubsController(ClubsService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<ClubEventDto>>> List([FromQuery] string? kind, [FromQuery] bool upcoming = true, CancellationToken ct = default)
        => Ok(await service.ListAsync(kind, upcoming, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClubEventDto>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    [HttpGet("{id:guid}/rsvps")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<ActionResult<List<ClubEventRsvpDto>>> ListRsvps(Guid id, CancellationToken ct)
        => Ok(await service.ListRsvpsAsync(id, ct));

    [HttpPost]
    [Authorize(Roles = "admin,teacher")]
    public async Task<ActionResult<ClubEventDto>> Create([FromBody] CreateEventRequest req, CancellationToken ct)
        => Ok(await service.CreateAsync(req, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<ActionResult<ClubEventDto>> Update(Guid id, [FromBody] UpdateEventRequest req, CancellationToken ct)
        => Ok(await service.UpdateAsync(id, req, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken ct)
    {
        await service.DeleteAsync(id, ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/rsvp")]
    public async Task<ActionResult<ClubEventRsvpDto>> Rsvp(Guid id, [FromBody] RsvpRequest req, CancellationToken ct)
        => Ok(await service.RsvpAsync(id, req, ct));

    [HttpDelete("{id:guid}/rsvp")]
    public async Task<ActionResult> CancelRsvp(Guid id, CancellationToken ct)
    {
        await service.CancelRsvpAsync(id, ct);
        return Ok(new { success = true });
    }

    [HttpPost("{id:guid}/attendance")]
    [Authorize(Roles = "admin,teacher")]
    public async Task<ActionResult> MarkAttendance(Guid id, [FromBody] MarkAttendanceRequest req, CancellationToken ct)
    {
        await service.MarkAttendanceAsync(id, req, ct);
        return Ok(new { success = true });
    }
}
