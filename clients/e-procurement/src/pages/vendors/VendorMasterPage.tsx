import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { searchVendors, type VendorListItemDto } from "@/api/suppliers";
import { useSwec } from "@/api/swec";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { EmptyState, Spinner } from "@/components/ui";
import { StatusBadge, TypeBadge } from "@/components/vendors/badges";
import { formatVendorType, isSwecType } from "@/lib/format";
import { FshPermissions } from "@/lib/fsh-permissions";

const REGIONS = ["Peninsular", "Sarawak", "Sabah"];

export function VendorMasterPage({
  onOpen,
  onNavigate,
}: {
  onOpen: (id: string) => void;
  onNavigate: (key: string) => void;
}) {
  const { data: swec } = useSwec();
  const [q, setQ] = useState("");
  const [type, setType] = useState("all");
  const [region, setRegion] = useState("all");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  const { data, isPending, isFetching } = useQuery({
    queryKey: ["vendors", q, page, pageSize],
    queryFn: () =>
      searchVendors({
        search: q.trim() || undefined,
        pageNumber: page,
        pageSize,
      }),
  });

  const vendors = useMemo(() => {
    const items = data?.items ?? [];
    return items.filter((v) => {
      const t = formatVendorType(v.type);
      return (type === "all" || t === type) && (region === "all" || v.region === region);
    });
  }, [data?.items, type, region]);

  const swecCount = vendors.filter((v) => isSwecType(v.type)).length;
  const totalCount = data?.totalCount ?? 0;
  const totalPages = data?.totalPages ?? 1;

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Vendor Master</h1>
          <p>
            Centralised supplier register — categories, contacts, addresses, currencies, banking,
            compliance, and performance.
          </p>
        </div>
        <div className="spacer" />
        <Gated permission={FshPermissions.vendors.create}>
          <button type="button" className="btn btn-pri btn-sm" onClick={() => onNavigate("vendors/new")}>
            <Icon name="plus" size={15} /> New vendor
          </button>
        </Gated>
      </div>

      <div className="ribbon">
        {totalCount} vendors on file
        {vendors.length !== totalCount ? ` · ${vendors.length} match filters on this page` : null}
        {" · "}
        {swecCount} SWEC · {vendors.length - swecCount} Non-SWEC
        {isFetching && !isPending ? " · refreshing…" : null}
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search vendor / code</label>
              <input
                type="text"
                value={q}
                placeholder="name or SWK-V-…"
                onChange={(e) => {
                  setQ(e.target.value);
                  setPage(1);
                }}
              />
            </div>
            <div className="field" style={{ margin: 0 }}>
              <label>Type</label>
              <select value={type} onChange={(e) => setType(e.target.value)}>
                <option value="all">All</option>
                <option value="SWEC">SWEC</option>
                <option value="Non-SWEC">Non-SWEC</option>
              </select>
            </div>
            <div className="field" style={{ margin: 0, minWidth: 170 }}>
              <label>Region</label>
              <select value={region} onChange={(e) => setRegion(e.target.value)}>
                <option value="all">All</option>
                {REGIONS.map((r) => (
                  <option key={r} value={r}>
                    {r}
                  </option>
                ))}
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading vendors…" />
        ) : (
          <>
            <table>
              <thead>
                <tr>
                  <th>Vendor</th>
                  <th>Type</th>
                  <th>SWEC codes</th>
                  <th>Region</th>
                  <th className="amt">Rating</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {vendors.map((v: VendorListItemDto) => (
                  <tr key={v.id} className="drillrow" onClick={() => onOpen(v.id)}>
                    <td>
                      <div style={{ fontWeight: 700 }}>{v.name}</div>
                      <div className="hint">{v.code}</div>
                    </td>
                    <td>
                      <TypeBadge type={v.type} />
                    </td>
                    <td style={{ whiteSpace: "normal", maxWidth: 280 }}>
                      {(v.categories ?? []).map((c) => (
                        <span key={c} className="swchip" title={swec?.path(c)}>
                          {swec?.label(c) ?? c}
                        </span>
                      ))}
                    </td>
                    <td>
                      {v.region}
                      <div className="hint">{v.state}</div>
                    </td>
                    <td className="amt">★ {v.rating}</td>
                    <td>
                      <StatusBadge status={v.status} />
                    </td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        Open <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
                {vendors.length === 0 ? (
                  <tr>
                    <td colSpan={7}>
                      <EmptyState>No vendors match the filter.</EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
            {totalPages > 1 ? (
              <div className="actionbar" style={{ padding: "12px 16px" }}>
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </button>
                <span className="hint" style={{ margin: "0 12px" }}>
                  Page {page} of {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </button>
              </div>
            ) : null}
          </>
        )}
      </div>
      <p className="hint" style={{ marginTop: 10 }}>
        {vendors.length} vendor(s) shown
      </p>
    </>
  );
}
