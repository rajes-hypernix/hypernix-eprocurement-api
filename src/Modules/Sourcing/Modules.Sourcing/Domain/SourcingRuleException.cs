using System.Net;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Sourcing.Domain;

/// <summary>Thrown for illegal Sourcing state transitions or violated business rules.</summary>
public sealed class SourcingRuleException(string message) : CustomException(message, (IEnumerable<string>?)null, HttpStatusCode.Conflict);
