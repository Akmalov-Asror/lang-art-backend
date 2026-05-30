using LangArt.Api.Features.Speaking.Dto;

namespace LangArt.Api.Features.Speaking;

/// <summary>
/// Transcribes a recorded audio submission and assigns rubric grades.
/// Implementations may call OpenAI Whisper + Claude (the real path) or
/// return deterministic-ish stubs (the dev path used today).
/// </summary>
public interface IAiSpeechGradingService
{
    Task<AiGradeResult> GradeAsync(string audioUrl, string promptText, CancellationToken ct);
}
