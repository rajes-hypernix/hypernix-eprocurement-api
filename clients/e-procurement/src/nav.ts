export interface NavItem {
  key: string;
  icon: string;
  label: string;
  /** Old AUTHORIZATION-MATRIX action — mapped to FSH permissions via gateNav/`Gated`. */
  action?: string;
  /** Optional navigate target (default `/${key}`). Use for query deep-links. */
  href?: string;
  /** Nested sidebar links (expandable dropdown). */
  children?: NavItem[];
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
      { key: "audits", icon: "eye", label: "Audit log", action: "ViewAudits" },
      {
        key: "masters",
        icon: "list",
        label: "Masters",
        action: "ManageMasters",
        href: "/masters",
        children: [
          { key: "masters-banks", icon: "clip", label: "Banks", href: "/masters?tab=banks", action: "ManageLookups" },
          { key: "masters-lists", icon: "list", label: "Lists", href: "/masters?tab=lists", action: "ManageLookups" },
          {
            key: "masters-countries",
            icon: "field",
            label: "Countries",
            href: "/masters?tab=countries",
            action: "ManageLookups",
          },
          { key: "masters-org", icon: "users", label: "Org", href: "/masters?tab=org", action: "ManageLookups" },
          {
            key: "masters-settings",
            icon: "menu",
            label: "Settings",
            href: "/masters?tab=settings",
            action: "ManageConfiguration",
          },
          {
            key: "masters-currencies",
            icon: "clip",
            label: "Currencies",
            href: "/masters?tab=currencies",
            action: "ManageConfiguration",
          },
          {
            key: "masters-rates",
            icon: "chart",
            label: "Exchange rates",
            href: "/masters?tab=rates",
            action: "ManageConfiguration",
          },
          {
            key: "masters-tax",
            icon: "hash",
            label: "Tax codes",
            href: "/masters?tab=tax",
            action: "ManageConfiguration",
          },
          {
            key: "masters-payment",
            icon: "send",
            label: "Payment terms",
            href: "/masters?tab=payment",
            action: "ManageConfiguration",
          },
          {
            key: "masters-incoterms",
            icon: "flag",
            label: "Incoterms",
            href: "/masters?tab=incoterms",
            action: "ManageConfiguration",
          },
          {
            key: "masters-locations",
            icon: "field",
            label: "Locations",
            href: "/masters?tab=locations",
            action: "ManageConfiguration",
          },
          {
            key: "masters-items",
            icon: "box",
            label: "Items",
            href: "/masters?tab=items",
            action: "ManageConfiguration",
          },
          {
            key: "masters-numbering",
            icon: "list",
            label: "Numbering",
            href: "/masters?tab=numbering",
            action: "ManageConfiguration",
          },
        ],
      },
      // Custom Fields / Segments / Entry Forms — deferred (blank Coming Soon); hide until built.
    ],
  },
];

export const VENDOR_CENTER_TABS: CenterTab[] = [
  {
    key: "portal",
    label: "Vendor portal",
    items: [
      { key: "dashboard", icon: "dashboard", label: "Dashboard", action: "ViewMyRfqs" },
      { key: "bids", icon: "box", label: "My Bids", action: "ViewMyRfqs" },
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

