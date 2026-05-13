using NpgsqlTypes;

namespace LangArt.Api.Data.Enums;

public enum PaymentStatus
{
    [PgName("completed")] Completed,
    [PgName("pending")] Pending,
    [PgName("failed")] Failed,
}
