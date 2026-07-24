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

    public static class Views
    {
        public const string Resource = "Platform.Views";
        public const string View = $"Permissions.{Resource}.View";
        public const string ManageOwn = $"Permissions.{Resource}.ManageOwn";
        public const string ManageShared = $"Permissions.{Resource}.ManageShared";
    }

    public static class Configuration
    {
        public const string Resource = "Platform.Configuration";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
    }

    public static class CustomFields
    {
        public const string Resource = "Platform.CustomFields";
        public const string View = $"Permissions.{Resource}.View";
        public const string Manage = $"Permissions.{Resource}.Manage";
    }

    public static class EntryForms
    {
        public const string Resource = "Platform.EntryForms";
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
        new("View Configuration", ActionConstants.View, Configuration.Resource, IsBasic: true),
        new("Manage Configuration", "Manage", Configuration.Resource),
        new("View Custom Fields", ActionConstants.View, CustomFields.Resource, IsBasic: true),
        new("Manage Custom Fields", "Manage", CustomFields.Resource),
        new("View Entry Forms", ActionConstants.View, EntryForms.Resource, IsBasic: true),
        new("Manage Entry Forms", "Manage", EntryForms.Resource),

        // Using saved views is all-principal (View/ManageOwn are Basic); publishing/editing a
        // shared or system view is a step up, granted explicitly (Buyer today, Admin by root).
        new("View Saved Views", ActionConstants.View, Views.Resource, IsBasic: true),
        new("Manage Own Saved Views", "ManageOwn", Views.Resource, IsBasic: true),
        new("Manage Shared Saved Views", "ManageShared", Views.Resource),
    ];
}
