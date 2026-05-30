namespace LangArt.Api.Data.Entities;

/// <summary>
/// Persistent record of a Wisdom Lug'ati import run. Stored so admins can see
/// the history of past imports across backend restarts (the in-memory job
/// dictionary in WisdomImportService is wiped on restart).
/// </summary>
public class WisdomImportJob
{
    public Guid Id { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string State { get; set; } = "running";   // running, completed, failed, cancelled
    public string? Error { get; set; }
    public string Letters { get; set; } = "";
    public string? CurrentLetter { get; set; }
    public int CurrentPage { get; set; }
    public int LastPage { get; set; }
    public int LettersDone { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int UnchangedExisting { get; set; }
    public int Skipped { get; set; }
    public int FailedFetches { get; set; }
    public int MinStar { get; set; }
    public Guid OwnerId { get; set; }
}
