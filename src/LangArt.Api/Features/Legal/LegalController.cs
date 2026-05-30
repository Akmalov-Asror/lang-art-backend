using LangArt.Api.Features.Legal.Dto;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Legal;

[ApiController]
[Route("api/legal")]
[Authorize]
public class LegalController(LegalService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<LegalDocumentDto>>> List([FromQuery] string? kind, CancellationToken ct)
        => Ok(await service.ListAsync(kind, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<LegalDocumentDto>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetAsync(id, ct));

    [HttpGet("pending/me")]
    public async Task<ActionResult<PendingAcceptanceDto>> Pending(CancellationToken ct)
        => Ok(await service.GetPendingForMeAsync(ct));

    [HttpPost("accept")]
    public async Task<ActionResult<LegalAcceptanceDto>> Accept([FromBody] AcceptDocumentRequest req, CancellationToken ct)
        => Ok(await service.AcceptAsync(req, ct));

    [HttpPost("publish")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<LegalDocumentDto>> Publish([FromBody] PublishDocumentRequest req, CancellationToken ct)
        => Ok(await service.PublishAsync(req, ct));

    [HttpGet("{id:guid}/acceptances")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<List<LegalAcceptanceDto>>> Acceptances(Guid id, CancellationToken ct)
        => Ok(await service.ListAcceptancesAsync(id, ct));
}
