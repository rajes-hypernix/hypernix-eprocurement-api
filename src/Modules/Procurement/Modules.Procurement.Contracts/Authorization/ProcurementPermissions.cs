using FSH.Framework.Shared.Constants;

namespace FSH.Modules.Procurement.Contracts.Authorization;

public static class ProcurementPermissions
{
    public static class PurchaseOrders
    {
        public const string Resource = "Procurement.PurchaseOrders";
        public const string View = $"Permissions.{Resource}.View";
        public const string CreateFromAward = $"Permissions.{Resource}.CreateFromAward";
        public const string Issue = $"Permissions.{Resource}.Issue";
        public const string Acknowledge = $"Permissions.{Resource}.Acknowledge";
    }

    public static class Deliveries
    {
        public const string Resource = "Procurement.Deliveries";
        public const string View = $"Permissions.{Resource}.View";
        public const string CreateAsn = $"Permissions.{Resource}.CreateAsn";
        public const string Receive = $"Permissions.{Resource}.Receive";
    }

    public static class Invoices
    {
        public const string Resource = "Procurement.Invoices";
        public const string View = $"Permissions.{Resource}.View";
        public const string Submit = $"Permissions.{Resource}.Submit";
        public const string Approve = $"Permissions.{Resource}.Approve";
        public const string ResolveException = $"Permissions.{Resource}.ResolveException";
    }

    public static IReadOnlyList<FshPermission> All { get; } =
    [
        new("View Purchase Orders", ActionConstants.View, PurchaseOrders.Resource, IsBasic: true),
        new("Create POs from Award", "CreateFromAward", PurchaseOrders.Resource),
        new("Issue Purchase Order", "Issue", PurchaseOrders.Resource),
        new("Acknowledge Purchase Order", "Acknowledge", PurchaseOrders.Resource),

        new("View Deliveries", ActionConstants.View, Deliveries.Resource, IsBasic: true),
        new("Create ASN", "CreateAsn", Deliveries.Resource),
        new("Receive ASN", "Receive", Deliveries.Resource),

        new("View Invoices", ActionConstants.View, Invoices.Resource, IsBasic: true),
        new("Submit Invoice", "Submit", Invoices.Resource),
        new("Approve Invoice", "Approve", Invoices.Resource),
        new("Resolve Invoice Exception", "ResolveException", Invoices.Resource),
    ];
}
