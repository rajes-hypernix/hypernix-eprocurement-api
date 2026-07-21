import { useParams } from "react-router-dom";
import { NAV_LABELS } from "@/nav";
import { ApiSmokePanel } from "@/components/ApiSmokePanel";

/** Phase 0/1 placeholder — real screens land from Phase 2 onward. */
export function PlaceholderPage() {
  const { pageKey = "dashboard" } = useParams();
  const label = NAV_LABELS[pageKey] ?? pageKey;
  const isDashboard = pageKey === "dashboard";

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>{label}</h1>
          <p>{isDashboard ? "Shell + API foundation. Open Vendor Master from Setup to manage suppliers." : "Coming in a later slice."}</p>
        </div>
      </div>
      <div className="card">
        <div className="cbody">
          <p className="muted">
            {isDashboard
              ? "Feature pages are not migrated yet. The smoke panel below proves JWT + /api/v1 calls work."
              : "This screen is not migrated yet. Feature ports start in Phase 2 (Vendor Master / Platform)."}
          </p>
        </div>
      </div>
      {isDashboard ? <ApiSmokePanel /> : null}
    </>
  );
}
