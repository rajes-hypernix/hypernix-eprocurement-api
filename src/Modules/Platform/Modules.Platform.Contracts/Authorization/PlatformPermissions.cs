using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Platform.Contracts.Authorization;

public static class PlatformPermissions
{
    public static class Lookups
    {
        public const string Resource = "Platform.Lookups";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
    }

    public static class CustomLists
    {
        public const string Resource = "Platform.CustomLists";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
    }

    public static class Org
    {
        public const string Resource = "Platform.Org";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
    }

    public static class FormTemplates
    {
        public const string Resource = "Platform.FormTemplates";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Lookups", ActionConstants.View, Lookups.Resource, IsBasic: true),
        new("Manage Lookups", "Manage", Lookups.Resource),
        new("View Custom Lists", ActionConstants.View, CustomLists.Resource, IsBasic: true),
        new("Manage Custom Lists", "Manage", CustomLists.Resource),
        new("View Org Units", ActionConstants.View, Org.Resource, IsBasic: true),
        new("Manage Org Units", "Manage", Org.Resource),
        new("View Form Templates", ActionConstants.View, FormTemplates.Resource, IsBasic: true),
        new("Manage Form Templates", "Manage", FormTemplates.Resource),
    ];
}
