using System.Net;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Platform.Domain;

public sealed class PlatformRuleException(string message)
    : CustomException(message, (IEnumerable<string>?)null, HttpStatusCode.Conflict);
