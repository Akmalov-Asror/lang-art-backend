using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LangArt.Api.Common.Filters;

/// <summary>
/// Wraps every successful action result in <c>{ success: true, data, message? }</c>.
/// Mirrors the NestJS controller pattern of manually returning <c>{ success, data }</c>.
/// 4xx/5xx responses flow through <see cref="Middleware.ExceptionHandlingMiddleware"/> instead.
/// </summary>
public class ApiResponseFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is ObjectResult obj && IsSuccessStatus(obj.StatusCode))
        {
            // Already wrapped — leave it alone (e.g., legacy controllers that return ApiResponse directly).
            if (obj.Value is ApiResponse || (obj.Value is not null && obj.Value.GetType().IsGenericType && obj.Value.GetType().GetGenericTypeDefinition() == typeof(ApiResponse<>)))
            {
                await next();
                return;
            }

            var wrapped = new ApiResponse<object?>
            {
                Success = true,
                Data = obj.Value,
            };
            context.Result = new ObjectResult(wrapped)
            {
                StatusCode = obj.StatusCode ?? StatusCodes.Status200OK,
            };
        }
        else if (context.Result is EmptyResult || (context.Result is StatusCodeResult sc && IsSuccessStatus(sc.StatusCode)))
        {
            context.Result = new ObjectResult(new ApiResponse<object?> { Success = true })
            {
                StatusCode = StatusCodes.Status200OK,
            };
        }

        await next();
    }

    private static bool IsSuccessStatus(int? statusCode) =>
        statusCode is null || (statusCode >= 200 && statusCode < 300);
}
