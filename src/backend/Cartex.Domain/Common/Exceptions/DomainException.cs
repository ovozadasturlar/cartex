namespace Cartex.Domain.Common.Exceptions;

/// The base of every business failure the API turns into problem+json. The standard exception
/// constructors are deliberately absent: a domain error without its code cannot be mapped to a
/// response, so there is no valid way to construct one without saying which rule was broken.
public abstract class DomainException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string message, string code = "not_found") : DomainException(message, code);

public sealed class ConflictException(string message, string code = "conflict") : DomainException(message, code);

public sealed class ForbiddenException(string message, string code = "forbidden") : DomainException(message, code);

public sealed class BusinessRuleException(string message, string code = "business_rule") : DomainException(message, code);
