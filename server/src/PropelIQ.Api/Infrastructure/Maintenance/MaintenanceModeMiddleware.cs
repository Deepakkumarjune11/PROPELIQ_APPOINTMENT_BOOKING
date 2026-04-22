using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PropelIQ.Api.Infrastructure.Maintenance;

/// <summary>
/// Short-circuits all incoming requests with HTTP 503 + <c>Retry-After: 300</c>
/// when maintenance mode is active, except for paths listed in
/// <see cref="MaintenanceModeOptions.ExemptPaths"/>.
///
/// Pipeline placement: AFTER <c>UseExceptionHandler</c> (so unhandled middleware
/// exceptions are still caught) and BEFORE <c>UseAuthorization</c> (so
/// unauthenticated users see maintenance page rather than 401, and auth middleware
/// is not exercised unnecessarily during maintenance).
/// </summary>
public sealed class MaintenanceModeMiddleware(
    RequestDelegate next,
    IMaintenanceModeService maintenance,
    IOptions<MaintenanceModeOptions> opts,
    ILogger<MaintenanceModeMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var path        = context.Request.Path.Value ?? string.Empty;
        var exemptPaths = opts.Value.ExemptPaths;

        // Exempt paths are always allowed through (health check, admin maintenance, swagger)
        foreach (var exempt in exemptPaths)
        {
            if (path.StartsWith(exempt, StringComparison.OrdinalIgnoreCase))
            {
                await next(context).ConfigureAwait(false);
                return;
            }
        }

        if (await maintenance.IsActiveAsync(context.RequestAborted).ConfigureAwait(false))
        {
            MaintenanceStatus status;
            try
            {
                status = await maintenance.GetStatusAsync(context.RequestAborted)
                    .ConfigureAwait(false);
            }
            catch (StackExchange.Redis.RedisException ex)
            {
                logger.LogWarning(ex,
                    "Redis unavailable while reading maintenance status — passing request through");
                await next(context).ConfigureAwait(false);
                return;
            }

            logger.LogInformation("Request blocked by maintenance mode: {Path}", path);

            context.Response.StatusCode  = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            context.Response.Headers.Append("Retry-After", "300");

            var body = new MaintenanceModeResponse(
                Message:          "System is under maintenance. Please try again later.",
                StartedAtUtc:     status.StartedAtUtc,
                EstimatedMinutes: status.EstimatedMinutes);

            await context.Response.WriteAsJsonAsync(body, context.RequestAborted)
                .ConfigureAwait(false);
            return;
        }

        await next(context).ConfigureAwait(false);
    }
}
