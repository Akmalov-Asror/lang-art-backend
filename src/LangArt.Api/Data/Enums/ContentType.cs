using NpgsqlTypes;

namespace LangArt.Api.Data.Enums;

public enum ContentType
{
    [PgName("text")] Text,
    [PgName("video")] Video,
    [PgName("audio")] Audio,
    [PgName("slide")] Slide,
    [PgName("exercise")] Exercise,
}
