namespace PMP.Shared.Exceptions;

/// <summary>
/// Thrown when a domain rule or business rule (BRULE-*) is violated.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }
}
