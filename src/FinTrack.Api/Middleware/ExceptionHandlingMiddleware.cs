using System.Text.Json;
using FinTrack.Application.Common.Exceptions;

namespace FinTrack.Api.Middleware;

/// <summary>
/// Maps application exceptions onto RFC 9457 ProblemDetails:
///   BusinessRuleException → 400/403/409 (stable `code` field consumed by the Angular client),
///   NotFoundException      → 404,
///   everything else        → 500 (message hidden).
/// </summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (BusinessRuleException ex)
        {
            await WriteProblemAsync(context, ex.StatusCode, ex.Code, ex.Message);
        }
        catch (NotFoundException ex)
        {
            await WriteProblemAsync(context, StatusCodes.Status404NotFound, "NOT_FOUND", ex.Message);
        }
        catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
        {
            logger.LogWarning(ex, "Database-level conflict (unique/concurrency) on {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status409Conflict, "CONCURRENT_UPDATE",
                "The request conflicts with another operation; retry with fresh state.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception while processing {Method} {Path}",
                context.Request.Method, context.Request.Path);
            await WriteProblemAsync(context, StatusCodes.Status500InternalServerError,
                "INTERNAL_ERROR", "An unexpected error occurred. The incident has been logged.");
        }
    }

    private static async Task WriteProblemAsync(HttpContext context, int statusCode, string code, string detail)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var payload = new
        {
            type = $"https://docs.fintrack.example/problems/{code.ToLowerInvariant()}",
            title = code,
            status = statusCode,
            code,
            detail
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
