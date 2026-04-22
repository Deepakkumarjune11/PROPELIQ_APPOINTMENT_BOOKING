namespace ClinicalIntelligence.Application.Exceptions;

/// <summary>Thrown when a resource is not found — maps to HTTP 404.</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>Thrown when a request is semantically invalid — maps to HTTP 422.</summary>
public sealed class UnprocessableEntityException : Exception
{
    public UnprocessableEntityException(string message) : base(message) { }
}

/// <summary>Thrown when the caller does not own the requested resource — maps to HTTP 403.</summary>
public sealed class ForbiddenException : Exception
{
    public ForbiddenException(string message) : base(message) { }
}
