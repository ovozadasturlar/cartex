namespace Cartex.Domain.Common.Exceptions;

public abstract class DomainException(string message, string code) : Exception(message)
{
    public string Code { get; } = code;
}

public sealed class NotFoundException(string message, string code = "not_found") : DomainException(message, code);

public sealed class ConflictException(string message, string code = "conflict") : DomainException(message, code);

public sealed class ForbiddenException(string message, string code = "forbidden") : DomainException(message, code);

public sealed class BusinessRuleException(string message, string code = "business_rule") : DomainException(message, code);
