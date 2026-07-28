using Mediator;

namespace FSH.Modules.Identity.Contracts.v1.ResolveTenant;

public sealed record ResolveTenantByEmailQuery(string Email) : IQuery<ResolveTenantByEmailResponse>;

public sealed record ResolveTenantCandidateDto(string TenantId, string? TenantName);

public sealed record ResolveTenantByEmailResponse(
    ResolveTenantStatus Status,
    string? TenantId,
    string? TenantName,
    IReadOnlyList<ResolveTenantCandidateDto>? Candidates);

public enum ResolveTenantStatus
{
    Found = 0,
    NotFound = 1,
    Ambiguous = 2,
}
