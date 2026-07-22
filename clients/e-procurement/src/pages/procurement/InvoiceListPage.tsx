import { useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { listInvoices } from "@/api/procurement";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { InvoiceStatusBadge } from "@/components/procurement/badges";
import { fmt, dateMY } from "@/lib/format";

function shortId(id: string): string {
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

export function InvoiceListPage({ onOpen }: { onOpen: (id: string) => void }) {
  const [q, setQ] = useState("");

  const { data, isPending } = useQuery({
    queryKey: ["invoices"],
    queryFn: listInvoices,
  });

  const rows = useMemo(() => {
    const items = data ?? [];
    if (!q.trim()) return items;
    const needle = q.trim().toLowerCase();
    return items.filter(
      (i) => `${i.code} ${i.invoiceNo} ${i.poId} ${i.status}`.toLowerCase().includes(needle),
    );
  }, [data, q]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Invoices</h1>
          <p>Vendor-submitted invoices matched against received goods.</p>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search code / invoice no. / PO</label>
              <input value={q} onChange={(e) => setQ(e.target.value)} placeholder="INV-2026-…" />
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading invoices…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Invoice no.</th>
                <th>PO</th>
                <th className="amt">Total</th>
                <th>Status</th>
                <th>Date</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rows.map((i) => (
                <tr key={i.id} className="drillrow" onClick={() => onOpen(i.id)}>
                  <td style={{ fontWeight: 700 }}>{i.code}</td>
                  <td>{i.invoiceNo}</td>
                  <td>{shortId(i.poId)}</td>
                  <td className="amt">{fmt(i.total)}</td>
                  <td>
                    <InvoiceStatusBadge status={i.status} />
                  </td>
                  <td>{dateMY(i.invoiceDate)}</td>
                  <td className="amt">
                    <span className="btn btn-ghost btn-sm">
                      Open <Icon name="chev" size={13} />
                    </span>
                  </td>
                </tr>
              ))}
              {rows.length === 0 ? (
                <tr>
                  <td colSpan={7}>
                    <EmptyState>No invoices match the filter.</EmptyState>
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
