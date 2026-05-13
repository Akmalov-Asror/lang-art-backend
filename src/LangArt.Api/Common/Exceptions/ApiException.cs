namespace LangArt.Api.Common.Exceptions;

/// <summary>
/// Base class for exceptions that should turn into a structured 4xx response.
/// Equivalent to NestJS's HttpException family.
/// </summary>
public abstract class ApiException : Exception
{
    public abstract int StatusCode { get; }
    public string? ErrorCode { get; }

    protected ApiException(string message, string? errorCode = null) : base(message)
    {
        ErrorCode = errorCode;
    }
}

public class BadRequestException : ApiException
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
    public BadRequestException(string message = "Bad Request", string? errorCode = null) : base(message, errorCode) { }
}

public class UnauthorizedException : ApiException
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
    public UnauthorizedException(string message = "Unauthorized", string? errorCode = null) : base(message, errorCode) { }
}

public class ForbiddenException : ApiException
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
    public ForbiddenException(string message = "Forbidden", string? errorCode = null) : base(message, errorCode) { }
}

public class NotFoundException : ApiException
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public NotFoundException(string message = "Not Found", string? errorCode = null) : base(message, errorCode) { }
}

public class ConflictException : ApiException
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public ConflictException(string message = "Conflict", string? errorCode = null) : base(message, errorCode) { }
}
