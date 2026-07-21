import { useQuery } from "@tanstack/react-query";
import { searchVendors, listSwecCategories } from "@/api/suppliers";
import { listRequisitions } from "@/api/sourcing";
import { getUnreadCount } from "@/api/notifications";
import { ApiRequestError } from "@/lib/api-client";

/**
 * Phase 1 exit smoke — hits a few real `/api/v1/...` GETs after login.
 * Shown on the Dashboard placeholder only; remove or shrink once Phase 2 screens land.
 */
export function ApiSmokePanel() {
  const vendors = useQuery({
    queryKey: ["smoke", "suppliers", "vendors"],
    queryFn: () => searchVendors({ pageNumber: 1, pageSize: 5 }),
  });
  const swec = useQuery({
    queryKey: ["smoke", "suppliers", "swec"],
    queryFn: listSwecCategories,
  });
  const requisitions = useQuery({
    queryKey: ["smoke", "sourcing", "requisitions"],
    queryFn: listRequisitions,
  });
  const unread = useQuery({
    queryKey: ["smoke", "notifications", "unread-count"],
    queryFn: getUnreadCount,
  });

  const rows: { label: string; status: "ok" | "loading" | "error"; detail: string }[] = [
    statusRow("GET /api/v1/suppliers/vendors", vendors, (d) => `${d.totalCount} vendors`),
    statusRow("GET /api/v1/suppliers/swec", swec, (d) => `${d.length} SWEC categories`),
    statusRow("GET /api/v1/sourcing/requisitions", requisitions, (d) => `${d.length} requisitions`),
    statusRow("GET /api/v1/notifications/unread-count", unread, (d) => `unread=${d}`),
  ];

  const allOk = rows.every((r) => r.status === "ok");
  const anyError = rows.some((r) => r.status === "error");

  return (
    <div className="card" style={{ marginTop: 16 }}>
      <div className="chead">
        <h3>Phase 1 — API smoke</h3>
      </div>
      <div className="cbody">
        <p className="muted" style={{ marginTop: 0 }}>
          {allOk
            ? "All smoke calls succeeded — Phase 1 API foundation is working."
            : anyError
              ? "One or more smoke calls failed — see detail (401 usually means proxy hit HTTP:5030 and lost the Bearer on HTTPS redirect; restart Vite after the proxy fix)."
              : "Calling /api/v1 endpoints…"}
        </p>
        <table style={{ width: "100%", fontSize: 13 }}>
          <thead>
            <tr>
              <th align="left">Endpoint</th>
              <th align="left">Status</th>
              <th align="left">Detail</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((r) => (
              <tr key={r.label}>
                <td>
                  <code>{r.label}</code>
                </td>
                <td>{r.status}</td>
                <td className="muted">{r.detail}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function statusRow<T>(
  label: string,
  q: { isPending: boolean; isError: boolean; error: unknown; data: T | undefined },
  format?: (data: T) => string,
): { label: string; status: "ok" | "loading" | "error"; detail: string } {
  if (q.isPending) return { label, status: "loading", detail: "…" };
  if (q.isError) {
    const err = q.error;
    const detail =
      err instanceof ApiRequestError
        ? `${err.status}: ${err.problem?.detail ?? err.message}${
            err.problem?.reason ? ` (${String(err.problem.reason)})` : ""
          }`
        : err instanceof Error
          ? err.message
          : String(err);
    return { label, status: "error", detail };
  }
  const detail = q.data !== undefined && format ? format(q.data) : "ok";
  return { label, status: "ok", detail };
}
