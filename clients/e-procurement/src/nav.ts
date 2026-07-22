export interface NavItem {
  key: string;
  icon: string;
  label: string;
  /** Old AUTHORIZATION-MATRIX action — mapped to FSH permissions via gateNav/`Gated`. */
  action?: string;
}

export interface NavGroup {
  title: string;
  items: NavItem[];
}

export interface CenterTab {
  key: string;
  label: string;
  items: NavItem[];
}

/** Adapter: the sidebar renderer keeps consuming NavGroup[] unchanged. */
export const tabsToNavGroups = (tabs: CenterTab[]): NavGroup[] =>
  tabs.map((t) => ({ title: t.label, items: t.items }));

/** Buyer nav — full original surface; deferred screens route to ComingSoon placeholders. */
export const BUYER_CENTER_TABS: CenterTab[] = [
  {
    key: "sourcing",
    label: "Sourcing",
    items: [
      { key: "dashboard", icon: "dashboard", label: "Dashboard", action: "UseDashboards" },
      { key: "views", icon: "eye", label: "Saved Views", action: "UseSavedViews" },
      { key: "reqs", icon: "doc", label: "Requisitions", action: "ViewRequisitions" },
      { key: "rfqs", icon: "rfq", label: "RFQs", action: "ViewRfqs" },
      { key: "awards", icon: "award", label: "Awards & POs", action: "ViewAwards" },
    ],
  },
  {
    key: "p2p",
    label: "Procure to Pay",
    items: [
      { key: "pos", icon: "box", label: "Purchase Orders", action: "ViewPos" },
      { key: "deliveries", icon: "send", label: "Deliveries", action: "ViewAsns" },
      { key: "invoices", icon: "doc", label: "Invoices", action: "ViewInvoices" },
      // PV placeholder rides the invoices read tier until payments get real actions.
      { key: "payments", icon: "download", label: "Payments", action: "ViewInvoices" },
      { key: "statements", icon: "clip", label: "Statements", action: "ViewStatements" },
    ],
  },
  {
    key: "evaluation",
    label: "Evaluation",
    items: [{ key: "openings", icon: "lock", label: "Bid Openings", action: "ViewBidOpenings" }],
  },
  {
    key: "setup",
    label: "Setup",
    items: [
      { key: "vendors", icon: "vendor", label: "Vendor Master", action: "ViewVendors" },
      { key: "onboarding", icon: "clip", label: "Onboarding", action: "ViewOnboarding" },
      { key: "forms", icon: "edit", label: "Forms", action: "ViewForms" },
    ],
  },
  {
    key: "communication",
    label: "Communication",
    items: [
      { key: "chats", icon: "msg", label: "Clarifications", action: "ViewClarifications" },
      { key: "notifications", icon: "bell", label: "Notifications", action: "ViewNotifications" },
    ],
  },
  {
    key: "administration",
    label: "Administration",
    items: [
      { key: "admin", icon: "users", label: "User Management", action: "ManageUsers" },
      { key: "lists", icon: "list", label: "Custom Lists", action: "ManageCustomLists" },
      { key: "customfields", icon: "field", label: "Custom Fields", action: "ManageCustomFields" },
      { key: "segments", icon: "chart", label: "Segments", action: "ManageSegments" },
      { key: "items", icon: "box", label: "Item Master", action: "ManageItems" },
      { key: "entryforms", icon: "form", label: "Entry Forms", action: "ManageEntryForms" },
      { key: "numbering", icon: "hash", label: "Numbering", action: "ManageNumbering" },
    ],
  },
];

export const VENDOR_CENTER_TABS: CenterTab[] = [
  {
    key: "portal",
    label: "Vendor portal",
    items: [
      { key: "dashboard", icon: "dashboard", label: "My RFQs", action: "ViewRfqs" },
      { key: "bids", icon: "box", label: "My Bids", action: "ViewRfqs" },
      { key: "pos", icon: "box", label: "Purchase Orders", action: "ViewPos" },
      { key: "deliveries", icon: "send", label: "Deliveries", action: "ViewAsns" },
      { key: "invoices", icon: "doc", label: "Invoices", action: "ViewInvoices" },
      { key: "statement", icon: "clip", label: "Statement", action: "ViewStatements" },
      { key: "chats", icon: "msg", label: "Clarifications", action: "ViewClarifications" },
      { key: "notifications", icon: "bell", label: "Notifications", action: "ViewNotifications" },
    ],
  },
];

export const BUYER_NAV: NavGroup[] = tabsToNavGroups(BUYER_CENTER_TABS);
export const VENDOR_NAV: NavGroup[] = tabsToNavGroups(VENDOR_CENTER_TABS);

/** Buyer labels win on shared keys (e.g. dashboard) so PlaceholderPage titles stay correct for buyers. */
export const NAV_LABELS: Record<string, string> = Object.fromEntries(
  [...VENDOR_NAV, ...BUYER_NAV].flatMap((g) => g.items.map((i) => [i.key, i.label])),
);
