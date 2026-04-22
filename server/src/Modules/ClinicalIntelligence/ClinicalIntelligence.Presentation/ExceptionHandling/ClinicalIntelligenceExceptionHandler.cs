using ClinicalIntelligence.Application.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace ClinicalIntelligence.Presentation.ExceptionHandling;

/// <summary>
/// Maps ClinicalIntelligence Application-layer exceptions to RFC 7807 ProblemDetails responses.
/// Registered alongside <c>PatientAccessExceptionHandler</c> in the ASP.NET Core pipeline.
/// </summary>
internal sealed class ClinicalIntelligenceExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext       httpContext,
        Exception         exception,
        CancellationToken cancellationToken)
    {
        int? statusCode = exception switch
        {
            NotFoundException            => StatusCodes.Status404NotFound,
            UnprocessableEntityException => StatusCodes.Status422UnprocessableEntity,
            ForbiddenException           => StatusCodes.Status403Forbidden,
            _                            => null,
        };

        if (statusCode is null)
            return false;

        var problemDetails = new ProblemDetails
        {
            Status = statusCode,
            Title  = exception switch
            {
                NotFoundException            => "Not Found",
                UnprocessableEntityException => "Unprocessable Entity",
                ForbiddenException           => "Forbidden",
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
