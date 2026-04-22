using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using PatientAccess.Application.Exceptions;

namespace PatientAccess.Presentation.ExceptionHandling;

/// <summary>
/// Maps Application-layer domain exceptions to RFC 7807 ProblemDetails responses.
/// Registered via <c>services.AddExceptionHandler&lt;PatientAccessExceptionHandler&gt;()</c>.
/// Works in conjunction with <c>app.UseExceptionHandler()</c> and <c>services.AddProblemDetails()</c>
/// configured in Program.cs.
/// </summary>
internal sealed class PatientAccessExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext     httpContext,
        Exception       exception,
        CancellationToken cancellationToken)
    {
        int? statusCode = exception switch
        {
            NotFoundException            => StatusCodes.Status404NotFound,
            ConflictException            => StatusCodes.Status409Conflict,
            UnprocessableEntityException => StatusCodes.Status422UnprocessableEntity,
            _                            => null,
        };

        if (statusCode is null)
            return false; // Let the default handler produce a 500

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title  = exception switch
            {
                NotFoundException            => "Not Found",
                ConflictException            => "Conflict",
                UnprocessableEntityException => "Unprocessable Entity",
                _                            => "Error",
            },
            Detail = exception.Message,
        };

        httpContext.Response.StatusCode  = statusCode.Value;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
