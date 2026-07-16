using System.Net;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Suppliers.Domain.Onboarding;

/// <summary>Thrown for illegal onboarding state transitions or violated onboarding business rules.</summary>
public sealed class OnboardingRuleException(string message) : CustomException(message, (IEnumerable<string>?)null, HttpStatusCode.Conflict);
