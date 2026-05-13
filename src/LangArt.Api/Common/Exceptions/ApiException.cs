namespace LangArt.Api.Common.Exceptions;

/// <summary>
/// Base class for exceptions that should turn into a structured 4xx response.
/// Equivalent to NestJS's HttpException family.
/// </summary>
public abstract class ApiException : Exception
{
    public abstract int StatusCode { get; }
    public string? ErrorCode { get; }
    public object? Payload { get; }

    protected ApiException(string message, string? errorCode = null, object? payload = null) : base(message)
    {
        ErrorCode = errorCode;
        Payload = payload;
    }
}

public class BadRequestException : ApiException
{
    public override int StatusCode => StatusCodes.Status400BadRequest;
    public BadRequestException(string message = "Bad Request", string? errorCode = null, object? payload = null) : base(message, errorCode, payload) { }
}

public class UnauthorizedException : ApiException
{
    public override int StatusCode => StatusCodes.Status401Unauthorized;
    public UnauthorizedException(string message = "Unauthorized", string? errorCode = null, object? payload = null) : base(message, errorCode, payload) { }
}

public class ForbiddenException : ApiException
{
    public override int StatusCode => StatusCodes.Status403Forbidden;
    public ForbiddenException(string message = "Forbidden", string? errorCode = null, object? payload = null) : base(message, errorCode, payload) { }
}

public class NotFoundException : ApiException
{
    public override int StatusCode => StatusCodes.Status404NotFound;
    public NotFoundException(string message = "Not Found", string? errorCode = null, object? payload = null) : base(message, errorCode, payload) { }
}

public class ConflictException : ApiException
{
    public override int StatusCode => StatusCodes.Status409Conflict;
    public ConflictException(string message = "Conflict", string? errorCode = null, object? payload = null) : base(message, errorCode, payload) { }
}
