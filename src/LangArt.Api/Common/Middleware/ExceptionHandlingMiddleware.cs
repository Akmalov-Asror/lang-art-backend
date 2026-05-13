using System.Text.Json;
using LangArt.Api.Common.Exceptions;
using LangArt.Api.Common.Filters;
using LangArt.Api.Common.Serialization;

namespace LangArt.Api.Common.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (ApiException ex)
        {
            await WriteAsync(ctx, ex.StatusCode, ex.Message, ex.ErrorCode, ex.Payload);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "JSON deserialization error");
            await WriteAsync(ctx, StatusCodes.Status400BadRequest, ex.Message, "invalid_json", null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await WriteAsync(ctx, StatusCodes.Status500InternalServerError, "Internal Server Error", "internal_error", null);
        }
    }

    private static async Task WriteAsync(HttpContext ctx, int statusCode, string message, string? errorCode, object? data)
    {
        if (ctx.Response.HasStarted) return;
        ctx.Response.Clear();
        ctx.Response.StatusCode = statusCode;
        ctx.Response.ContentType = "application/json";

        var payload = new ApiResponse<object?>
        {
            Success = false,
            Error = errorCode ?? StatusCodeToErrorName(statusCode),
            Message = message,
            Data = data,
        };
        await JsonSerializer.SerializeAsync(ctx.Response.Body, payload, JsonOptions);
    }

    private static string StatusCodeToErrorName(int statusCode) => statusCode switch
    {
        400 => "bad_request",
        401 => "unauthorized",
        403 => "forbidden",
        404 => "not_found",
        409 => "conflict",
        429 => "too_many_requests",
        _ => "internal_error",
    };

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var o = new JsonSerializerOptions();
        JsonConfig.Configure(o);
        return o;
    }
}
