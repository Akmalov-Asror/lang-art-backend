using LangArt.Api.Common.Filters;
using Microsoft.AspNetCore.Mvc;

namespace LangArt.Api.Common.Validation;

/// <summary>
/// Replaces the default ProblemDetails ModelState 400 with an envelope-shaped response,
/// so the frontend's API client sees the same `{ success: false, error, message }` for
/// validation failures as for any other 4xx.
/// </summary>
public static class ValidationProblemFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(kvp => kvp.Value!.Errors.Count > 0)
            .ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

        var firstMessage = errors.Values.SelectMany(e => e).FirstOrDefault() ?? "Validation failed";

        return new BadRequestObjectResult(new ApiResponse<object?>
        {
            Success = false,
            Error = "validation_failed",
            Message = firstMessage,
            Data = errors,
        });
    }
}
