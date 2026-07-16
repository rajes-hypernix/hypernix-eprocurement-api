using Mediator;

namespace FSH.Modules.Suppliers.Contracts.v1.Vendors;

/// <summary>Legacy/simple create path — stays Pending, Type NonSwec (mirrors old CreateAsync).</summary>
public sealed record CreateVendorCommand(
    string Name,
    string? Region = null,
    string? State = null,
    string? City = null) : ICommand<Guid>;
