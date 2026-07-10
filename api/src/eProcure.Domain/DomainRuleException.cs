namespace eProcure.Domain;

/// <summary>
/// Thrown when a domain guard rail is violated. Mapped by the API's exception
/// middleware to HTTP 409 (rule violation). Never weaken a rule to avoid this —
/// fix the caller (CLAUDE.md golden rule 7).
/// </summary>
public sealed class DomainRuleException(string message) : Exception(message);
