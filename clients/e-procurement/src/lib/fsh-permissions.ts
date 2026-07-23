/**
 * FSH permission strings (mirrors *Permissions.cs Contracts).
 * Used by the Phase 1 map and Phase 6 nav/`Gated` wiring.
 */
export const FshPermissions = {
  vendors: {
    view: "Permissions.Suppliers.Vendors.View",
    create: "Permissions.Suppliers.Vendors.Create",
    update: "Permissions.Suppliers.Vendors.Update",
    toggleStatus: "Permissions.Suppliers.Vendors.ToggleStatus",
  },
  swec: {
    view: "Permissions.Suppliers.Swec.View",
  },
  onboarding: {
    view: "Permissions.Suppliers.Onboarding.View",
    invite: "Permissions.Suppliers.Onboarding.Invite",
    revoke: "Permissions.Suppliers.Onboarding.Revoke",
    review: "Permissions.Suppliers.Onboarding.Review",
  },
  requisitions: {
    view: "Permissions.Sourcing.Requisitions.View",
    manage: "Permissions.Sourcing.Requisitions.Manage",
  },
  rfqs: {
    view: "Permissions.Sourcing.Rfqs.View",
    manageDraft: "Permissions.Sourcing.Rfqs.ManageDraft",
    manageLifecycle: "Permissions.Sourcing.Rfqs.ManageLifecycle",
    invite: "Permissions.Sourcing.Rfqs.Invite",
    rescind: "Permissions.Sourcing.Rfqs.Rescind",
    extend: "Permissions.Sourcing.Rfqs.Extend",
  },
  bids: {
    viewMine: "Permissions.Sourcing.Bids.ViewMine",
    respond: "Permissions.Sourcing.Bids.Respond",
  },
  evaluation: {
    viewOpening: "Permissions.Sourcing.Evaluation.ViewOpening",
    viewTechnical: "Permissions.Sourcing.Evaluation.ViewTechnical",
    openTechnical: "Permissions.Sourcing.Evaluation.OpenTechnical",
    openCommercial: "Permissions.Sourcing.Evaluation.OpenCommercial",
    score: "Permissions.Sourcing.Evaluation.Score",
    finalizeTechnical: "Permissions.Sourcing.Evaluation.FinalizeTechnical",
  },
  award: {
    view: "Permissions.Sourcing.Award.View",
    submit: "Permissions.Sourcing.Award.Submit",
    approve: "Permissions.Sourcing.Award.Approve",
  },
  clarifications: {
    view: "Permissions.Sourcing.Clarifications.View",
    send: "Permissions.Sourcing.Clarifications.Send",
  },
  purchaseOrders: {
    view: "Permissions.Procurement.PurchaseOrders.View",
    createFromAward: "Permissions.Procurement.PurchaseOrders.CreateFromAward",
    issue: "Permissions.Procurement.PurchaseOrders.Issue",
    acknowledge: "Permissions.Procurement.PurchaseOrders.Acknowledge",
  },
  deliveries: {
    view: "Permissions.Procurement.Deliveries.View",
    createAsn: "Permissions.Procurement.Deliveries.CreateAsn",
    receive: "Permissions.Procurement.Deliveries.Receive",
  },
  invoices: {
    view: "Permissions.Procurement.Invoices.View",
    submit: "Permissions.Procurement.Invoices.Submit",
    approve: "Permissions.Procurement.Invoices.Approve",
    resolveException: "Permissions.Procurement.Invoices.ResolveException",
  },
  statements: {
    view: "Permissions.Procurement.Statements.View",
    viewMine: "Permissions.Procurement.Statements.ViewMine",
  },
  lookups: {
    view: "Permissions.Platform.Lookups.View",
    manage: "Permissions.Platform.Lookups.Manage",
  },
  customLists: {
    view: "Permissions.Platform.CustomLists.View",
    manage: "Permissions.Platform.CustomLists.Manage",
  },
  org: {
    view: "Permissions.Platform.Org.View",
    manage: "Permissions.Platform.Org.Manage",
  },
  formTemplates: {
    view: "Permissions.Platform.FormTemplates.View",
    manage: "Permissions.Platform.FormTemplates.Manage",
  },
  notifications: {
    view: "Permissions.Notifications.Inbox.View",
    markRead: "Permissions.Notifications.Inbox.MarkRead",
  },
  users: {
    view: "Permissions.Users.View",
    create: "Permissions.Users.Create",
    update: "Permissions.Users.Update",
    manageRoles: "Permissions.Users.ManageRoles",
  },
  roles: {
    view: "Permissions.Roles.View",
    create: "Permissions.Roles.Create",
    update: "Permissions.Roles.Update",
    delete: "Permissions.Roles.Delete",
  },
  auditTrails: {
    view: "Permissions.AuditTrails.View",
  },
  views: {
    view: "Permissions.Platform.Views.View",
    manageOwn: "Permissions.Platform.Views.ManageOwn",
    manageShared: "Permissions.Platform.Views.ManageShared",
  },
} as const;
