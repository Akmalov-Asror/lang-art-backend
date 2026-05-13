using LangArt.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LangArt.Api.Features.Health;

[ApiController]
[Route("api/health")]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("")]
    public async Task<IActionResult> Full()
    {
        var dbStatus = await CheckDbAsync();
        var memory = GC.GetGCMemoryInfo();
        var process = System.Diagnostics.Process.GetCurrentProcess();

        return Ok(new
        {
            Status = dbStatus.Ok ? "ok" : "degraded",
            Info = new
            {
                Database = new { Status = dbStatus.Ok ? "up" : "down", Message = dbStatus.Message },
                Memory = new
                {
                    Status = "up",
                    HeapBytes = memory.HeapSizeBytes,
                    WorkingSetBytes = process.WorkingSet64,
                },
            },
        });
    }

    [HttpGet("ready")]
    public async Task<IActionResult> Ready()
    {
        var dbStatus = await CheckDbAsync();
        if (!dbStatus.Ok)
        {
            return StatusCode(503, new { Status = "down", Message = dbStatus.Message });
        }
        return Ok(new { Status = "ready" });
    }

    [HttpGet("live")]
    public IActionResult Live()
    {
        return Ok(new
        {
            Status = "ok",
            Timestamp = DateTime.UtcNow,
        });
    }

    private async Task<(bool Ok, string? Message)> CheckDbAsync()
    {
        try
        {
            await _db.Database.ExecuteSqlRawAsync("SELECT 1");
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }
}
