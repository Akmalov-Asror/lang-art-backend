using NpgsqlTypes;

namespace LangArt.Api.Data.Enums;

public enum AttendanceStatus
{
    [PgName("present")] Present,
    [PgName("absent")] Absent,
    [PgName("late")] Late,
    [PgName("excused")] Excused,
}
