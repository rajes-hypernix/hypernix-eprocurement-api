import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listRfqs, type RfqListItemDto } from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";

const RELEVANT = new Set(["Closed", "Evaluation", "Awarded"]);

type FacetKey = "envelope" | "status";
const FACETS: { key: FacetKey; label: string }[] = [
  { key: "envelope", label: "Envelope" },
  { key: "status", label: "Status" },
];
const EMPTY_FACETS: Record<FacetKey, string> = { envelope: "all", status: "all" };

function EnvelopeBadge({ envelope }: { envelope: string }) {
  return envelope === "Dual" ? (
    <span className="badge b-blue">
      <Icon name="lock" size={12} /> Dual
    </span>
  ) : (
    <span className="badge b-grey">Single</span>
  );
}

export function BidOpeningsListPage({ onOpen }: { onOpen: (id: string) => void }) {
  const [q, setQ] = useState("");
  const [facets, setFacets] = useState<Record<FacetKey, string>>({ ...EMPTY_FACETS });

  const { data, isPending } = useQuery({ queryKey: ["rfqs"], queryFn: listRfqs });
  const scoped = useMemo(() => (data ?? []).filter((r) => RELEVANT.has(r.status)), [data]);

  const facetOptions = useMemo(() => {
    const sets: Record<FacetKey, Set<string>> = { envelope: new Set(), status: new Set() };
    for (const r of scoped) {
      if (r.envelope) sets.envelope.add(r.envelope);
      if (r.status) sets.status.add(r.status);
    }
    return {
      envelope: [...sets.envelope].sort((a, b) => a.localeCompare(b)),
      status: [...sets.status].sort((a, b) => a.localeCompare(b)),
    };
  }, [scoped]);

  const rows = useMemo(() => {
    const needle = q.trim().toLowerCase();
    return scoped.filter((r) => {
      if (needle && ![r.code, r.title].some((v) => (v ?? "").toLowerCase().includes(needle))) return false;
      if (facets.envelope !== "all" && r.envelope !== facets.envelope) return false;
      if (facets.status !== "all" && r.status !== facets.status) return false;
      return true;
    });
  }, [scoped, q, facets]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Bid Openings</h1>
          <p>Closed RFQs ready to open and evaluate. Technical opens and scores before commercial unseals.</p>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar filterbar-auto">
            <div className="field" style={{ margin: 0, minWidth: 200 }}>
              <label htmlFor="opening-search">RFQ</label>
              <input
                id="opening-search"
                value={q}
                onChange={(e) => setQ(e.target.value)}
                placeholder="RFQ number or title…"
                aria-label="Search bid openings"
              />
            </div>
            {FACETS.map((f) => (
              <div className="field" style={{ margin: 0 }} key={f.key}>
                <label htmlFor={`opening-facet-${f.key}`}>{f.label}</label>
                <select
                  id={`opening-facet-${f.key}`}
                  aria-label={f.label}
                  value={facets[f.key]}
                  onChange={(e) => setFacets((prev) => ({ ...prev, [f.key]: e.target.value }))}
                >
                  <option value="all">All</option>
                  {facetOptions[f.key].map((v) => (
                    <option key={v} value={v}>
                      {v}
                    </option>
                  ))}
                </select>
              </div>
            ))}
            <div className="field" style={{ margin: 0 }}>
              <label>&nbsp;</label>
              <button type="button" className="freset" onClick={() => { setQ(""); setFacets({ ...EMPTY_FACETS }); }}>
                Reset
              </button>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>RFQ</th>
                <th>Title</th>
                <th>Envelope</th>
                <th className="amt">Bids</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((r: RfqListItemDto) => (
                <tr key={r.id} className="drillrow" onClick={() => onOpen(r.id)}>
                  <td style={{ fontWeight: 700, color: "var(--teal)" }}>{r.code}</td>
                  <td>{r.title}</td>
                  <td>
                    <EnvelopeBadge envelope={r.envelope} />
                  </td>
                  <td className="amt">{r.invitedCount}</td>
                  <td>
                    <span className="badge b-blue">{r.status}</span>
                  </td>
                  <td className="amt">
                    <span className="btn btn-ghost btn-sm" aria-label={`Open ${r.code}`}>
                      Open <Icon name="chev" size={13} />
                    </span>
                  </td>
                </tr>
              ))}
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={6}>
                    <EmptyState>{scoped.length === 0 ? "No RFQs ready to open." : "No RFQs match these filters."}</EmptyState>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
