using NpgsqlTypes;

namespace LangArt.Api.Data.Enums;

public enum Role
{
    [PgName("admin")] Admin,
    [PgName("teacher")] Teacher,
    [PgName("student")] Student,
}
