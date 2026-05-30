using LangArt.Api.Common.Auth;
using LangArt.Api.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Features.Vocabulary;

[ApiController]
[Route("api/admin/vocabulary/import-wisdom")]
[Authorize(Roles = "admin")]
public class WisdomImportController(WisdomImportService importer, ICurrentUser current) : ControllerBase
{
    public class StartImportRequest
    {
        /// <summary>Optional subset of letters, e.g. "ab" to test. Default: all a-z.</summary>
        public string? Letters { get; set; }
        /// <summary>Minimum word "star" rating to import. 0 = everything (default), 1+ = skip obscure entries.</summary>
        public int MinStar { get; set; } = 0;
    }

    [HttpPost("start")]
    public async Task<ActionResult<WisdomImportJob>> Start([FromBody] StartImportRequest? req)
    {
        var job = await importer.StartImportAsync(current.Id, req?.Letters, req?.MinStar ?? 0);
        return Ok(job);
    }

    [HttpGet("status/{jobId:guid}")]
    public async Task<ActionResult<WisdomImportJob>> Status(Guid jobId, CancellationToken ct)
    {
        var s = await importer.GetStatusAsync(jobId, ct);
        return s == null ? NotFound(new { error = "job not found" }) : Ok(s);
    }

    [HttpGet("jobs")]
    public async Task<ActionResult<List<WisdomImportJob>>> ListJobs(CancellationToken ct)
        => Ok(await importer.ListJobsAsync(ct));

    [HttpPost("cancel/{jobId:guid}")]
    public ActionResult Cancel(Guid jobId)
        => importer.CancelJob(jobId)
            ? Ok(new { success = true })
            : NotFound(new { error = "job not found or already finished" });
}
