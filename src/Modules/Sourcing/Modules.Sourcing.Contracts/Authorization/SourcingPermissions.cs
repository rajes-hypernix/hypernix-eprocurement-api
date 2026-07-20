using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Sourcing.Contracts.Authorization;

public static class SourcingPermissions
{
    public static class Requisitions
    {
        public const string Resource = "Sourcing.Requisitions";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
    }

    public static class Rfqs
    {
        public const string Resource = "Sourcing.Rfqs";
        public const string View = $"Permissions.{Resource}.View";
        public const string ManageDraft = $"Permissions.{Resource}.ManageDraft";
        public const string ManageLifecycle = $"Permissions.{Resource}.ManageLifecycle";
        public const string Invite = $"Permissions.{Resource}.Invite";
        public const string Rescind = $"Permissions.{Resource}.Rescind";
        public const string Extend = $"Permissions.{Resource}.Extend";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Requisitions", ActionConstants.View, Requisitions.Resource, IsBasic: true),
        new("Manage Requisitions", "Manage", Requisitions.Resource),
        new("View RFQs", ActionConstants.View, Rfqs.Resource, IsBasic: true),
        new("Manage RFQ Draft", "ManageDraft", Rfqs.Resource),
        new("Manage RFQ Lifecycle", "ManageLifecycle", Rfqs.Resource),
        new("Invite Vendor to RFQ", "Invite", Rfqs.Resource),
        new("Rescind RFQ Invitation", "Rescind", Rfqs.Resource),
        new("Extend RFQ", "Extend", Rfqs.Resource),
    ];
}
