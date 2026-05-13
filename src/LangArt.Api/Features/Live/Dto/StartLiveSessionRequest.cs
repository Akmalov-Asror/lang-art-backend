using System.ComponentModel.DataAnnotations;

namespace LangArt.Api.Features.Live.Dto;

public class StartLiveSessionRequest
{
    [Required]
    public Guid ClassroomId { get; set; }

    [Required]
    public Guid LessonId { get; set; }
}
