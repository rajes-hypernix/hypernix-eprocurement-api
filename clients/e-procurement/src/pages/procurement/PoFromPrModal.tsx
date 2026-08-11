import { useEffect, useMemo, useRef, useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import {
  createPurchaseOrderFromRequisition,
} from "@/api/procurement";
import {
  getEligibleRequisitionLinesForOrdering,
  listRequisitions,
  type EligibleOrderLineDto,
} from "@/api/sourcing";
import { searchVendors } from "@/api/suppliers";
import { Icon } from "@/components/Icon";
import { Modal, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";
import { fmt } from "@/lib/format";

type LineState = { include: boolean; qty: string; unitPrice: string };

const SOURCEABLE = new Set(["Submitted", "PartiallySourced", "PartiallyOrdered"]);

/**
 * POC PoFromPrModal — create a Draft PO directly from one requisition:
 * pick PR (optionally pre-selected), include Open lines (qty capped at remaining),
 * enter unit price, pick vendor → Create draft.
 */
export function PoFromPrModal({
  onClose,
  onCreated,
  initialPrId,
}: {
  onClose: () => void;
  onCreated: (id: string) => void;
  initialPrId?: string;
}) {
  const { data: prs = [], isPending: prsPending } = useQuery({
    queryKey: ["requisitions"],
    queryFn: listRequisitions,
  });
  const { data: eligible = [], isPending: linesPending } = useQuery({
    queryKey: ["eligible-order-lines"],
    queryFn: getEligibleRequisitionLinesForOrdering,
  });
  const { data: vendorsPage, isPending: vendorsPending } = useQuery({
    queryKey: ["vendors", "from-pr-modal"],
    queryFn: () => searchVendors({ pageNumber: 1, pageSize: 200, sortBy: "name", sortDir: "asc" }),
    staleTime: 60_000,
  });

  const [prId, setPrId] = useState("");
  const [vendorId, setVendorId] = useState("");
  const [lineState, setLineState] = useState<Record<string, LineState>>({});
  const [err, setErr] = useState<string | null>(null);

  const eligibleByPr = useMemo(() => {
    const m = new Map<string, EligibleOrderLineDto[]>();
    for (const l of eligible) {
      if (!l.sourceable || l.qtyRemaining <= 0) continue;
      const list = m.get(l.prId) ?? [];
      list.push(l);
      m.set(l.prId, list);
    }
    return m;
  }, [eligible]);

  const orderablePrs = useMemo(
    () =>
      prs.filter(
        (p) => SOURCEABLE.has(p.headerStatus) && (eligibleByPr.get(p.id)?.length ?? 0) > 0,
      ),
    [prs, eligibleByPr],
  );

  const openLines = eligibleByPr.get(prId) ?? [];
  const selectedPr = orderablePrs.find((p) => p.id === prId);
  const vendors = vendorsPage?.items ?? [];

  const pickPr = (idVal: string) => {
    setPrId(idVal);
    setErr(null);
    const lines = eligibleByPr.get(idVal) ?? [];
    setLineState(
      Object.fromEntries(
        lines.map((l) => [
          l.prLineId,
          {
            include: true,
            qty: String(l.qtyRemaining),
            unitPrice: String(l.estUnitPrice ?? ""),
          },
        ]),
      ),
    );
  };

  const preselected = useRef(false);
  useEffect(() => {
    if (preselected.current || !initialPrId || orderablePrs.length === 0) return;
    if (!orderablePrs.some((p) => p.id === initialPrId)) return;
    preselected.current = true;
    pickPr(initialPrId);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [initialPrId, orderablePrs.length]);

  const patchLine = (lid: string, patch: Partial<LineState>) =>
    setLineState((cur) => ({ ...cur, [lid]: { ...cur[lid], ...patch } }));

  const create = useMutation({
    mutationFn: () => {
      const lines = openLines
        .filter((l) => lineState[l.prLineId]?.include)
        .map((l) => {
          const s = lineState[l.prLineId]!;
          return {
            prLineId: l.prLineId,
            qty: Math.min(Number(s.qty) || 0, l.qtyRemaining),
            unitPrice: Number(s.unitPrice) || 0,
            priceConfirmed: true,
          };
        })
        .filter((l) => l.qty > 0);

      if (!prId) {
        setErr("Pick a requisition.");
        throw new Error("__handled__");
      }
      if (!vendorId) {
        setErr("Pick a vendor.");
        throw new Error("__handled__");
      }
      if (lines.length === 0) {
        setErr("Include at least one line with a quantity.");
        throw new Error("__handled__");
      }

      return createPurchaseOrderFromRequisition({
        prId,
        vendorId,
        currency: "MYR",
        lines,
      });
    },
    onSuccess: (id) => onCreated(id),
    onError: (e: Error) => {
      if (e.message === "__handled__") return;
      setErr(e instanceof ApiRequestError ? e.message : e.message);
    },
  });

  const loading = prsPending || linesPending || vendorsPending;

  return (
    <Modal
      wide
      title="New PO from requisition"
      icon="doc"
      footer={
        <>
          <button type="button" className="btn btn-out" onClick={onClose} disabled={create.isPending}>
            Cancel
          </button>
          <button
            type="button"
            className="btn btn-pri"
            disabled={create.isPending || loading}
            onClick={() => {
              setErr(null);
              create.mutate();
            }}
          >
            <Icon name="check" size={14} /> {create.isPending ? "Creating…" : "Create draft"}
          </button>
        </>
      }
    >
      {err ? (
        <Notice tone="error" icon="x" style={{ marginBottom: 12 }}>
          {err}
        </Notice>
      ) : null}

      {loading ? <Spinner label="Loading requisitions…" /> : null}

      {!loading ? (
        <>
          <div className="field">
            <label>Requisition</label>
            <select value={prId} onChange={(e) => pickPr(e.target.value)}>
              <option value="">Pick a Submitted / Partially sourced PR</option>
              {orderablePrs.map((p) => (
                <option key={p.id} value={p.id}>
                  {p.code} — {p.department || "—"}
                </option>
              ))}
            </select>
            {orderablePrs.length === 0 ? (
              <p className="hint" style={{ marginTop: 6 }}>
                No orderable requisitions right now (need Open lines with remaining qty).
              </p>
            ) : null}
          </div>

          {selectedPr ? (
            <>
              <table style={{ margin: "12px 0", width: "100%" }}>
                <thead>
                  <tr>
                    <th style={{ width: 34 }} aria-label="Include" />
                    <th>Item</th>
                    <th>Description</th>
                    <th className="amt">Remaining</th>
                    <th className="amt" style={{ minWidth: 110 }}>
                      Qty
                    </th>
                    <th className="amt" style={{ minWidth: 110 }}>
                      Unit price
                    </th>
                  </tr>
                </thead>
                <tbody>
                  {openLines.map((l) => {
                    const s = lineState[l.prLineId];
                    if (!s) return null;
                    return (
                      <tr key={l.prLineId}>
                        <td>
                          <input
                            type="checkbox"
                            checked={s.include}
                            onChange={(e) => patchLine(l.prLineId, { include: e.target.checked })}
                            aria-label={`Include ${l.itemCode}`}
                          />
                        </td>
                        <td>{l.itemCode}</td>
                        <td>{l.description}</td>
                        <td className="amt">{fmt(l.qtyRemaining)}</td>
                        <td>
                          <input
                            type="number"
                            min={0}
                            max={l.qtyRemaining}
                            step="any"
                            value={s.qty}
                            disabled={!s.include}
                            onChange={(e) => patchLine(l.prLineId, { qty: e.target.value })}
                          />
                        </td>
                        <td>
                          <input
                            type="number"
                            min={0}
                            step="any"
                            value={s.unitPrice}
                            disabled={!s.include}
                            onChange={(e) => patchLine(l.prLineId, { unitPrice: e.target.value })}
                          />
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
              <p className="hint" style={{ margin: "0 0 12px" }}>
                Quantities are capped at each line&apos;s remaining balance; the rest stays open on the PR.
              </p>
            </>
          ) : null}

          <div className="field" style={{ marginBottom: 0 }}>
            <label>Vendor</label>
            <select value={vendorId} onChange={(e) => setVendorId(e.target.value)}>
              <option value="">Pick a vendor</option>
              {vendors.map((v) => (
                <option key={v.id} value={v.id}>
                  {v.code} — {v.name}
                </option>
              ))}
            </select>
          </div>
        </>
      ) : null}
    </Modal>
  );
}
