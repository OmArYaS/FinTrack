namespace FinTrack.Application.Common.Exceptions;

/// <summary>
/// Violation of a banking business rule. Carries a stable machine-readable code
/// that is surfaced to clients through RFC 9457 ProblemDetails and matched by
/// stored procedures via SQL THROW codes.
/// </summary>
public sealed class BusinessRuleException : Exception
{
    public string Code { get; }

    public int StatusCode { get; }

    public BusinessRuleException(string code, string message, int statusCode = 400)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
    }
}

/// <summary-Requested aggregate or entity does not exist (maps to HTTP 404).</summary>
public sealed class NotFoundException : Exception
{
    public NotFoundException(string entityName, object key)
        : base($"{entityName} '{key}' was not found.")
    {
    }
}
