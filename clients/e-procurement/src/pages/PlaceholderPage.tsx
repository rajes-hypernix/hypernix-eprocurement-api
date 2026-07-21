import { useParams } from "react-router-dom";
import { NAV_LABELS } from "@/nav";

/** Phase 0 placeholder — real screens land in later phases. */
export function PlaceholderPage() {
  const { pageKey = "dashboard" } = useParams();
  const label = NAV_LABELS[pageKey] ?? pageKey;

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>{label}</h1>
          <p>Coming in a later slice.</p>
        </div>
      </div>
      <div className="card">
        <div className="cbody">
          <p className="muted">
            Phase 0 shell only — this screen is not migrated yet. Auth, layout, and navigation are
            live; feature pages follow in later phases.
          </p>
        </div>
      </div>
    </>
  );
}
