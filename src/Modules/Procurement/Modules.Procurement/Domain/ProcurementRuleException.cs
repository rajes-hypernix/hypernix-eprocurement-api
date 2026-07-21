using System.Net;
using FSH.Framework.Core.Exceptions;

namespace FSH.Modules.Procurement.Domain;

public sealed class ProcurementRuleException(string message) : CustomException(message, (IEnumerable<string>?)null, HttpStatusCode.Conflict);
