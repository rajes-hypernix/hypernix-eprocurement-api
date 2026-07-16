namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record OnboardingApproveResultDto(Guid VendorId, string VendorCode, string? DuplicateWarning);
