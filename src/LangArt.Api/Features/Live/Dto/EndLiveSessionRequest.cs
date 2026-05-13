namespace LangArt.Api.Features.Live.Dto;

public class EndLiveSessionRequest
{
    /// <summary>One of: <c>teacher_ended</c>, <c>timeout</c>, <c>server_restart</c>. Defaults to <c>teacher_ended</c> on the server when null.</summary>
    public string? Reason { get; set; }
}
