using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Suppliers.Contracts.Authorization;

public static class SuppliersPermissions
{
    public static class Vendors
    {
        public const string Resource = "Suppliers.Vendors";
        public const string View = $"Permissions.{Resource}.View";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Vendors", ActionConstants.View, Vendors.Resource, IsBasic: true),
    ];
}
