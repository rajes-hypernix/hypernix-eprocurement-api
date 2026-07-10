namespace eProcure.Application;

/// <summary>Thrown when a requested resource does not exist. Mapped to HTTP 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Thrown when the current principal may not access a resource. Mapped to HTTP 403.</summary>
public sealed class ForbiddenException(string message) : Exception(message);
