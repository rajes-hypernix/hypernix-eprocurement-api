import { useParams } from "react-router-dom";
import { NAV_LABELS } from "@/nav";

const FSH_ADMIN_NOTE =
  "Use the FSH admin app for this configuration until the e-procurement screen is ported.";

/** Per-route copy for deferred / Wave 2 screens. */
export const COMING_SOON_PAGES: Record<
  string,
  { title: string; description?: string; note?: string }
> = {
  payments: {
    title: "Payment Vouchers",
    description: "Supplier remittance and payment release.",
    note: "No payment vouchers — coming soon (deferred in the current scope).",
  },
  views: {
    title: "Saved Views",
    description: "Personal and shared list views across sourcing records.",
    note: "Saved views are deferred — this screen will land in a later wave.",
  },
  forms: {
    title: "Forms",
    description: "Form template library for onboarding and transactions.",
    note: FSH_ADMIN_NOTE,
  },
  admin: {
    title: "User Management",
    description: "Roles and access for e-procurement users.",
    note: FSH_ADMIN_NOTE,
  },
  lists: {
    title: "Custom Lists",
    description: "Lookup values and reason codes.",
    note: FSH_ADMIN_NOTE,
  },
  customfields: {
    title: "Custom Fields",
    description: "Extend records with configurable fields.",
    note: FSH_ADMIN_NOTE,
  },
  segments: {
    title: "Segments",
    description: "Chart of accounts segments and dimensions.",
    note: FSH_ADMIN_NOTE,
  },
  items: {
    title: "Item Master",
    description: "Catalog items referenced on requisitions and POs.",
    note: FSH_ADMIN_NOTE,
  },
  entryforms: {
    title: "Entry Forms",
    description: "Layout templates for data entry screens.",
    note: FSH_ADMIN_NOTE,
  },
  numbering: {
    title: "Numbering",
    description: "Document number sequences and prefixes.",
    note: FSH_ADMIN_NOTE,
  },
};

export type ComingSoonPageProps = {
  title: string;
  description?: string;
  note?: string;
};

/** Old PvPlaceholder-style deferred screen — pagehead + muted card body. */
export function ComingSoonPage({ title, description, note }: ComingSoonPageProps) {
  return (
    <>
      <div className="pagehead">
        <div>
          <h1>{title}</h1>
          {description ? <p>{description}</p> : null}
        </div>
      </div>
      <div className="card">
        <div className="cbody" style={{ textAlign: "center", padding: "40px 20px" }}>
          <p className="muted">
            {note ?? "Coming soon — deferred in the current scope."}
          </p>
        </div>
      </div>
    </>
  );
}

/** Resolves title/description/note from `COMING_SOON_PAGES` or nav labels. */
export function ComingSoonRoute({ pageKey }: { pageKey: string }) {
  const cfg = COMING_SOON_PAGES[pageKey];
  const title = cfg?.title ?? NAV_LABELS[pageKey] ?? pageKey;
  return (
    <ComingSoonPage
      title={title}
      description={cfg?.description}
      note={cfg?.note}
    />
  );
}

/** Route element — reads `:pageKey` when no explicit key is passed. */
export function ComingSoonFromParams() {
  const { pageKey = "dashboard" } = useParams();
  return <ComingSoonRoute pageKey={pageKey} />;
}
