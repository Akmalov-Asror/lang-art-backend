using LangArt.Api.Features.Writing.Dto;

namespace LangArt.Api.Features.Writing;

/// <summary>
/// Scores a student's written response against an IELTS-style 4-axis rubric:
/// Task Achievement, Coherence &amp; Cohesion, Grammar, Vocabulary. Returns
/// 0-100 per axis plus a supportive feedback paragraph.
/// </summary>
public interface IAiWritingGradingService
{
    Task<AiWritingGradeResult> GradeAsync(string text, string promptText, int? minWordCount, CancellationToken ct);
}
