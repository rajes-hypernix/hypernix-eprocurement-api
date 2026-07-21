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

    public static class Evaluation
    {
        public const string Resource = "Sourcing.Evaluation";
        public const string ViewOpening = $"Permissions.{Resource}.ViewOpening";
        public const string OpenTechnical = $"Permissions.{Resource}.OpenTechnical";
        public const string OpenCommercial = $"Permissions.{Resource}.OpenCommercial";
        public const string Score = $"Permissions.{Resource}.Score";
        public const string FinalizeTechnical = $"Permissions.{Resource}.FinalizeTechnical";
        public const string ViewTechnical = $"Permissions.{Resource}.ViewTechnical";
    }

    public static class Award
    {
        public const string Resource = "Sourcing.Award";
        public const string View = $"Permissions.{Resource}.View";
        public const string Submit = $"Permissions.{Resource}.Submit";
        public const string Approve = $"Permissions.{Resource}.Approve";
    }

    public static class Clarifications
    {
        public const string Resource = "Sourcing.Clarifications";
        public const string View = $"Permissions.{Resource}.View";
        public const string Send = $"Permissions.{Resource}.Send";
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

        // Evaluation/Award/Clarifications deliberately do NOT mark View as IsBasic — unlike the
        // resources above, these gate sealed-bid data (vendor identity + pricing behind the
        // technical/commercial envelope gates). Granting them to every Basic user by default
        // would undermine the evaluator-masking model; only Buyer/Approver/TechEvaluator/
        // CommEvaluator/Admin get them, via explicit role grants.
        new("View Bid Opening Status", "ViewOpening", Evaluation.Resource),
        new("Open Technical Envelope", "OpenTechnical", Evaluation.Resource),
        new("Open Commercial Envelope", "OpenCommercial", Evaluation.Resource),
        new("Score Technical Evaluation", "Score", Evaluation.Resource),
        new("Finalize Technical Evaluation", "FinalizeTechnical", Evaluation.Resource),
        new("View Technical Evaluation", "ViewTechnical", Evaluation.Resource),

        new("View Award", ActionConstants.View, Award.Resource),
        new("Submit Award", "Submit", Award.Resource),
        new("Approve Award", "Approve", Award.Resource),

        new("View Clarifications", ActionConstants.View, Clarifications.Resource),
        new("Send Clarification", "Send", Clarifications.Resource),
    ];
}
