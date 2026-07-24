import { useMemo, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { getEligibleRequisitionLinesForOrdering, type EligibleOrderLineDto } from "@/api/sourcing";
import { createPurchaseOrderFromRequisition } from "@/api/procurement";
import { searchVendors, type VendorListItemDto } from "@/api/suppliers";
import { Icon } from "@/components/Icon";
import { Notice, Spinner } from "@/components/ui";
import { fmt } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

type BasketLine = {
  prId: string;
  prCode: string;
  prLineId: string;
  itemCode: string;
  description: string;
  uom: string;
  qty: number;
  unitPrice: number;
};

type VendorBasket = {
  key: number;
  vendorId: string | null;
  vendorLabel: string;
  lines: BasketLine[];
};

let basketKeySeq = 1;

function VendorPicker({ onPick }: { onPick: (v: VendorListItemDto) => void }) {
  const [search, setSearch] = useState("");
  const { data } = useQuery({
    queryKey: ["vendor-search", search],
    queryFn: () => searchVendors({ search: search || undefined, pageSize: 8 }),
    enabled: search.trim().length > 1,
  });
  const results = data?.items ?? [];

  return (
    <div>
      <input
        type="text"
        placeholder="Search vendor by name or code…"
        value={search}
        onChange={(e) => setSearch(e.target.value)}
        style={{ marginBottom: 8 }}
      />
      {results.map((v) => (
        <div className="pickrow" key={v.id}>
          <div style={{ flex: 1 }}>
            <div style={{ fontWeight: 600, fontSize: 13 }}>{v.name}</div>
            <div className="hint">{v.code}</div>
          </div>
          <button type="button" className="btn btn-out btn-sm" onClick={() => onPick(v)}>
            Select
          </button>
        </div>
      ))}
      {search.trim().length > 1 && results.length === 0 ? <div className="hint">No vendors match.</div> : null}
    </div>
  );
}

function BasketCard({
  basket,
  onPickVendor,
  onChangeLine,
  onRemoveLine,
  onRemoveBasket,
}: {
  basket: VendorBasket;
  onPickVendor: (v: VendorListItemDto) => void;
  onChangeLine: (prLineId: string, patch: Partial<Pick<BasketLine, "qty" | "unitPrice">>) => void;
  onRemoveLine: (prLineId: string) => void;
  onRemoveBasket: () => void;
}) {
  const [pickingVendor, setPickingVendor] = useState(basket.vendorId === null);
  const total = basket.lines.reduce((s, l) => s + l.qty * l.unitPrice, 0);

  return (
    <div className="card" style={{ marginBottom: 12 }}>
      <div className="chead">
        <h3>
          {basket.vendorId && !pickingVendor ? (
            <>
              <Icon name="box" size={14} /> {basket.vendorLabel}
            </>
          ) : (
            "Pick a vendor for this basket"
          )}
        </h3>
        <div style={{ display: "flex", gap: 8 }}>
          {basket.vendorId && !pickingVendor ? (
            <button type="button" className="btn btn-ghost btn-sm" onClick={() => setPickingVendor(true)}>
              Change vendor
            </button>
          ) : null}
          <button type="button" className="btn btn-ghost btn-sm" onClick={onRemoveBasket}>
            <Icon name="x" size={13} /> Remove basket
          </button>
        </div>
      </div>
      <div className="cbody">
        {pickingVendor ? (
          <VendorPicker
            onPick={(v) => {
              onPickVendor(v);
              setPickingVendor(false);
            }}
          />
        ) : basket.lines.length === 0 ? (
          <p className="hint" style={{ margin: 0 }}>
            No lines yet — click <b>+</b> on an eligible line to add it here.
          </p>
        ) : (
          <table>
            <thead>
              <tr>
                <th>Item</th>
                <th className="amt">Qty</th>
                <th className="amt">Unit price</th>
                <th className="amt">Line total</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {basket.lines.map((l) => (
                <tr key={l.prLineId}>
                  <td>
                    <div style={{ fontWeight: 600 }}>{l.itemCode}</div>
                    <div className="hint">
                      {l.description} · from {l.prCode}
                    </div>
                  </td>
                  <td className="amt">
                    <input
                      type="number"
                      min={0.01}
                      step="0.01"
                      value={l.qty}
                      onChange={(e) => onChangeLine(l.prLineId, { qty: Number(e.target.value) })}
                      style={{ width: 90, textAlign: "right" }}
                    />{" "}
                    {l.uom}
                  </td>
                  <td className="amt">
                    <input
                      type="number"
                      min={0.01}
                      step="0.01"
                      value={l.unitPrice}
                      onChange={(e) => onChangeLine(l.prLineId, { unitPrice: Number(e.target.value) })}
                      style={{ width: 90, textAlign: "right" }}
                    />
                  </td>
                  <td className="amt">{fmt(l.qty * l.unitPrice)}</td>
                  <td className="amt">
                    <button type="button" className="btn btn-ghost btn-sm" onClick={() => onRemoveLine(l.prLineId)}>
                      Remove
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
      {basket.lines.length > 0 ? (
        <div className="cl-foot">
          <div className="cl-summ">
            <span>
              <b>{basket.lines.length}</b> line{basket.lines.length === 1 ? "" : "s"}
            </span>
            <span>
              est. <b>{fmt(total)}</b>
            </span>
          </div>
        </div>
      ) : null}
    </div>
  );
}

/**
 * Order builder workspace — vendor-keyed basket variant of the RFQ Consolidate workspace.
 * Eligible lines are staged client-side only (no server reservation until Build, unlike the RFQ
 * basket): "ordering" only ever writes to Sourcing's reservation ledger at PO-creation time.
 * "+" always targets the most-recently-added basket; start a new basket to route lines to a
 * different vendor.
 */
export function OrderBuilderPage({ onBack, onBuilt }: { onBack: () => void; onBuilt: (ids: string[]) => void }) {
  const { data: eligible = [], isPending } = useQuery({
    queryKey: ["eligible-order-lines"],
    queryFn: getEligibleRequisitionLinesForOrdering,
  });

  const [baskets, setBaskets] = useState<VendorBasket[]>([]);
  const [err, setErr] = useState<string | null>(null);

  const committedByLine = useMemo(() => {
    const m = new Map<string, number>();
    baskets.forEach((b) => b.lines.forEach((l) => m.set(l.prLineId, (m.get(l.prLineId) ?? 0) + l.qty)));
    return m;
  }, [baskets]);

  const remaining = (l: EligibleOrderLineDto) => l.qtyRemaining - (committedByLine.get(l.prLineId) ?? 0);

  const byPr = useMemo(() => {
    const groups = new Map<string, { prCode: string; lines: EligibleOrderLineDto[] }>();
    eligible.forEach((l) => {
      const g = groups.get(l.prId) ?? { prCode: l.prCode, lines: [] };
      g.lines.push(l);
      groups.set(l.prId, g);
    });
    return [...groups.entries()];
  }, [eligible]);

  const addToLastBasket = (line: EligibleOrderLineDto) => {
    if (!line.sourceable || remaining(line) <= 0) return;
    setErr(null);
    setBaskets((prev) => {
      let next = prev;
      if (next.length === 0) {
        next = [{ key: basketKeySeq++, vendorId: null, vendorLabel: "", lines: [] }];
      }
      const idx = next.length - 1;
      const target = next[idx]!;
      if (target.lines.some((l) => l.prLineId === line.prLineId)) return next;
      const qty = remaining(line);
      const newLine: BasketLine = {
        prId: line.prId,
        prCode: line.prCode,
        prLineId: line.prLineId,
        itemCode: line.itemCode,
        description: line.description,
        uom: line.uom,
        qty,
        unitPrice: line.estUnitPrice,
      };
      return next.map((b, i) => (i === idx ? { ...b, lines: [...b.lines, newLine] } : b));
    });
  };

  const addNewBasket = () => setBaskets((prev) => [...prev, { key: basketKeySeq++, vendorId: null, vendorLabel: "", lines: [] }]);

  const build = useMutation({
    mutationFn: async () => {
      const ids: string[] = [];
      for (const basket of baskets) {
        if (!basket.vendorId || basket.lines.length === 0) continue;
        const byPrId = new Map<string, BasketLine[]>();
        basket.lines.forEach((l) => byPrId.set(l.prId, [...(byPrId.get(l.prId) ?? []), l]));
        for (const [prId, lines] of byPrId) {
          // eslint-disable-next-line no-await-in-loop
          const id = await createPurchaseOrderFromRequisition({
            prId,
            vendorId: basket.vendorId,
            currency: "MYR",
            lines: lines.map((l) => ({ prLineId: l.prLineId, qty: l.qty, unitPrice: l.unitPrice })),
          });
          ids.push(id);
        }
      }
      return ids;
    },
    onSuccess: (ids) => onBuilt(ids),
    onError: (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  const readyBaskets = baskets.filter((b) => b.vendorId && b.lines.length > 0);
  const nLines = readyBaskets.reduce((n, b) => n + b.lines.length, 0);

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Purchase Orders
        </button>{" "}
        <Icon name="chev" size={13} /> <span>Order Builder</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>Order Builder</h1>
          <p>
            Group eligible requisition lines into vendor baskets and create a direct purchase order per vendor. Vendor and
            price are assigned here, per basket — nothing is reserved until you build.
          </p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x" style={{ marginBottom: 12 }}>
          {err}
        </Notice>
      ) : null}

      <div className="cl-wrap">
        <div className="cl-pane">
          <div className="cl-paneh">
            <h3>Eligible requisition lines</h3>
            <span className="ct">
              {byPr.length} PR{byPr.length === 1 ? "" : "s"}
            </span>
          </div>
          <div className="cl-body">
            {isPending ? <Spinner label="Loading eligible lines…" /> : null}
            {!isPending && byPr.length === 0 ? <div className="cl-empty">No requisition lines are currently eligible for ordering.</div> : null}
            {byPr.map(([prId, g]) => (
              <div className="prg" key={prId}>
                <div className="prg-h">
                  <span className="prg-meta">
                    <span className="prg-1">
                      <span className="prid">{g.prCode}</span>
                    </span>
                  </span>
                </div>
                {g.lines.map((l) => {
                  const rem = remaining(l);
                  const disabled = !l.sourceable || rem <= 0;
                  return (
                    <div className={`prg-line ${disabled ? "used" : ""}`} key={l.prLineId}>
                      <span className="lc">
                        <div className="desc">{l.description}</div>
                        <div className="sub">
                          {l.itemCode} · {rem} of {l.qty} {l.uom} remaining
                          {l.ineligibleReason ? ` · ${l.ineligibleReason}` : ""}
                        </div>
                      </span>
                      <button
                        type="button"
                        className="cl-add"
                        disabled={disabled}
                        title={l.ineligibleReason ?? (rem <= 0 ? "Fully committed" : "Add to the current basket")}
                        aria-label={`Add ${l.itemCode} from ${g.prCode}`}
                        onClick={() => addToLastBasket(l)}
                      >
                        +
                      </button>
                    </div>
                  );
                })}
              </div>
            ))}
          </div>
        </div>

        <div className="cl-pane">
          <div className="cl-paneh">
            <h3>Vendor baskets</h3>
            <span className="ct">
              {nLines} line{nLines === 1 ? "" : "s"}
            </span>
            <div className="cl-paneh-actions">
              <button type="button" className="btn btn-out btn-sm" onClick={addNewBasket}>
                <Icon name="plus" size={13} /> New vendor basket
              </button>
            </div>
          </div>
          <div className="cl-body">
            {baskets.length === 0 ? (
              <div className="cl-empty">
                Click <b>+</b> on the left, or <b>New vendor basket</b>, to get started.
              </div>
            ) : (
              baskets.map((b) => (
                <BasketCard
                  key={b.key}
                  basket={b}
                  onPickVendor={(v) =>
                    setBaskets((prev) => prev.map((x) => (x.key === b.key ? { ...x, vendorId: v.id, vendorLabel: `${v.name} (${v.code})` } : x)))
                  }
                  onChangeLine={(prLineId, patch) =>
                    setBaskets((prev) =>
                      prev.map((x) =>
                        x.key === b.key ? { ...x, lines: x.lines.map((l) => (l.prLineId === prLineId ? { ...l, ...patch } : l)) } : x,
                      ),
                    )
                  }
                  onRemoveLine={(prLineId) =>
                    setBaskets((prev) => prev.map((x) => (x.key === b.key ? { ...x, lines: x.lines.filter((l) => l.prLineId !== prLineId) } : x)))
                  }
                  onRemoveBasket={() => setBaskets((prev) => prev.filter((x) => x.key !== b.key))}
                />
              ))
            )}
          </div>
          <div className="cl-foot">
            <div className="cl-summ">
              <span>
                <b>{readyBaskets.length}</b> PO{readyBaskets.length === 1 ? "" : "s"} to create
              </span>
            </div>
            <div style={{ flex: 1 }} />
            <button
              type="button"
              className="btn btn-pri"
              disabled={readyBaskets.length === 0 || build.isPending}
              onClick={() => build.mutate()}
            >
              Build order{readyBaskets.length === 1 ? "" : "s"} <Icon name="chev" size={14} />
            </button>
          </div>
        </div>
      </div>
    </>
  );
}
