namespace LangArt.Api.Common.Filters;

/// <summary>
/// Standard response envelope. All successful HTTP responses are wrapped in this shape
/// by <see cref="ApiResponseFilter"/>; error responses are wrapped by the exception middleware.
/// </summary>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public T? Data { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }

    public static ApiResponse<T> Ok(T data, string? message = null) => new()
    {
        Success = true,
        Data = data,
        Message = message,
    };
}

public class ApiResponse : ApiResponse<object> { }
