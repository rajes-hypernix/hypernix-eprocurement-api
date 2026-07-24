import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { createStandalonePurchaseOrder, type CreateStandalonePoLineInput } from "@/api/procurement";
import { searchVendors, type VendorListItemDto } from "@/api/suppliers";
import { Icon } from "@/components/Icon";
import { Notice } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";

type DraftLine = CreateStandalonePoLineInput & { key: number };
let keySeq = 1;
const emptyLine = (): DraftLine => ({ key: keySeq++, itemCode: "", description: "", uom: "EA", qty: 1, unitPrice: 0 });

export function PoStandaloneFormPage({ onBack, onCreated }: { onBack: () => void; onCreated: (id: string) => void }) {
  const [vendor, setVendor] = useState<VendorListItemDto | null>(null);
  const [vendorSearch, setVendorSearch] = useState("");
  const [currency, setCurrency] = useState("MYR");
  const [lines, setLines] = useState<DraftLine[]>([emptyLine()]);
  const [err, setErr] = useState<string | null>(null);

  const { data: vendorResults } = useQuery({
    queryKey: ["vendor-search", vendorSearch],
    queryFn: () => searchVendors({ search: vendorSearch || undefined, pageSize: 8 }),
    enabled: vendorSearch.trim().length > 1 && !vendor,
  });

  const patchLine = (key: number, patch: Partial<DraftLine>) =>
    setLines((prev) => prev.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  const removeLine = (key: number) => setLines((prev) => (prev.length > 1 ? prev.filter((l) => l.key !== key) : prev));

  const create = useMutation({
    mutationFn: () => {
      if (!vendor) throw new Error("Pick a vendor first.");
      return createStandalonePurchaseOrder({
        vendorId: vendor.id,
        currency,
        lines: lines.map(({ itemCode, description, uom, qty, unitPrice }) => ({ itemCode, description, uom, qty, unitPrice })),
      });
    },
    onSuccess: (id) => onCreated(id),
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  const canSubmit = Boolean(vendor) && lines.every((l) => l.itemCode.trim() && l.description.trim() && l.qty > 0 && l.unitPrice > 0);

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Purchase Orders
        </button>{" "}
        <Icon name="chev" size={13} /> <span>New standalone PO</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>New standalone purchase order</h1>
          <p>No requisition or award behind this PO — every field is entered here.</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x" style={{ marginBottom: 12 }}>
          {err}
        </Notice>
      ) : null}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Vendor</h3>
        </div>
        <div className="cbody">
          {vendor ? (
            <div className="pickrow">
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, fontSize: 13 }}>{vendor.name}</div>
                <div className="hint">{vendor.code}</div>
              </div>
              <button type="button" className="btn btn-ghost btn-sm" onClick={() => setVendor(null)}>
                Change
              </button>
            </div>
          ) : (
            <>
              <input
                type="text"
                placeholder="Search vendor by name or code…"
                value={vendorSearch}
                onChange={(e) => setVendorSearch(e.target.value)}
                style={{ marginBottom: 8 }}
              />
              {(vendorResults?.items ?? []).map((v) => (
                <div className="pickrow" key={v.id}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 600, fontSize: 13 }}>{v.name}</div>
                    <div className="hint">{v.code}</div>
                  </div>
                  <button type="button" className="btn btn-out btn-sm" onClick={() => setVendor(v)}>
                    Select
                  </button>
                </div>
              ))}
            </>
          )}
          <div className="field" style={{ marginTop: 12, maxWidth: 160 }}>
            <label>Currency</label>
            <input value={currency} onChange={(e) => setCurrency(e.target.value.toUpperCase())} maxLength={3} />
          </div>
        </div>
      </div>

      <div className="card">
        <div className="chead">
          <h3>Lines</h3>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Item code</th>
                <th>Description</th>
                <th>UoM</th>
                <th className="amt">Qty</th>
                <th className="amt">Unit price</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {lines.map((l) => (
                <tr key={l.key}>
                  <td>
                    <input value={l.itemCode} onChange={(e) => patchLine(l.key, { itemCode: e.target.value })} style={{ width: 110 }} />
                  </td>
                  <td>
                    <input value={l.description} onChange={(e) => patchLine(l.key, { description: e.target.value })} style={{ width: 200 }} />
                  </td>
                  <td>
                    <input value={l.uom} onChange={(e) => patchLine(l.key, { uom: e.target.value })} style={{ width: 60 }} />
                  </td>
                  <td className="amt">
                    <input
                      type="number"
                      min={0.01}
                      step="0.01"
                      value={l.qty}
                      onChange={(e) => patchLine(l.key, { qty: Number(e.target.value) })}
                      style={{ width: 80, textAlign: "right" }}
                    />
                  </td>
                  <td className="amt">
                    <input
                      type="number"
                      min={0.01}
                      step="0.01"
                      value={l.unitPrice}
                      onChange={(e) => patchLine(l.key, { unitPrice: Number(e.target.value) })}
                      style={{ width: 90, textAlign: "right" }}
                    />
                  </td>
                  <td className="amt">
                    <button type="button" className="btn btn-ghost btn-sm" disabled={lines.length === 1} onClick={() => removeLine(l.key)}>
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <button type="button" className="btn btn-out btn-sm" style={{ marginTop: 10 }} onClick={() => setLines((prev) => [...prev, emptyLine()])}>
            <Icon name="plus" size={13} /> Add line
          </button>
        </div>
      </div>

      <div className="actionbar" style={{ marginTop: 14 }}>
        <div className="spacer" style={{ flex: 1 }} />
        <button type="button" className="btn btn-pri" disabled={!canSubmit || create.isPending} onClick={() => create.mutate()}>
          Create purchase order
        </button>
      </div>
    </>
  );
}
