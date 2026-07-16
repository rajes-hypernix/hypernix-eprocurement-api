using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Suppliers.Contracts.Authorization;

public static class SuppliersPermissions
{
    public static class Vendors
    {
        public const string Resource = "Suppliers.Vendors";
        public const string View = $"Permissions.{Resource}.View";
        public const string Create = $"Permissions.{Resource}.Create";
        public const string Update = $"Permissions.{Resource}.Update";
        public const string ToggleStatus = $"Permissions.{Resource}.ToggleStatus";
    }

    public static class Swec
    {
        public const string Resource = "Suppliers.Swec";
        public const string View = $"Permissions.{Resource}.View";
    }

    public static class VendorUsers
    {
        public const string Resource = "Suppliers.VendorUsers";
        public const string View = $"Permissions.{Resource}.View";
    }

    public static class Onboarding
    {
        public const string Resource = "Suppliers.Onboarding";
        public const string View = $"Permissions.{Resource}.View";
        public const string Invite = $"Permissions.{Resource}.Invite";
        public const string Revoke = $"Permissions.{Resource}.Revoke";
        public const string Review = $"Permissions.{Resource}.Review";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Vendors", ActionConstants.View, Vendors.Resource, IsBasic: true),
        new("Create Vendors", ActionConstants.Create, Vendors.Resource),
        new("Update Vendors", ActionConstants.Update, Vendors.Resource),
        new("Toggle Vendor Status", "ToggleStatus", Vendors.Resource),
        new("View SWEC Taxonomy", ActionConstants.View, Swec.Resource, IsBasic: true),
        new("View Vendor Logins", ActionConstants.View, VendorUsers.Resource),
        new("View Onboarding", ActionConstants.View, Onboarding.Resource),
        new("Invite Onboarding", "Invite", Onboarding.Resource),
        new("Revoke Onboarding Invitation", "Revoke", Onboarding.Resource),
        new("Review Onboarding Application", "Review", Onboarding.Resource),
    ];
}
