import { useState } from "react";
import { Icon } from "@/components/Icon";
import { Spinner } from "@/components/ui";
import { useSwec, type SwecNode } from "@/api/swec";

export function SwecPicker({
  initial,
  vendorName,
  onCancel,
  onSave,
  busy = false,
}: {
  initial: string[];
  vendorName: string;
  onCancel: () => void;
  onSave: (codes: string[]) => void;
  busy?: boolean;
}) {
  const { data: swec } = useSwec();
  const [sel, setSel] = useState<Set<string>>(() => new Set(initial));
  const [kw, setKw] = useState("");

  const toggle = (code: string) => {
    setSel((prev) => {
      const next = new Set(prev);
      if (next.has(code)) next.delete(code);
      else next.add(code);
      return next;
    });
  };

  const matches = (n: SwecNode): boolean => {
    if (!kw) return true;
    const k = kw.toLowerCase();
    return (
      (n.name ?? "").toLowerCase().includes(k) ||
      (n.code ?? "").toLowerCase().includes(k) ||
      n.children.some(matches)
    );
  };

  const renderNodes = (nodes: SwecNode[], depth: number): React.ReactNode =>
    nodes.filter(matches).map((n) => (
      <div key={n.code}>
        <label className="swnode" style={{ paddingLeft: depth * 18 }}>
          <input
            type="checkbox"
            checked={sel.has(n.code)}
            onChange={() => toggle(n.code)}
            style={{ width: "auto" }}
          />
          <span style={{ fontWeight: n.isLeaf ? 400 : 700 }}>{n.name}</span>
          {n.isLeaf ? (
            <span className="hint" style={{ marginLeft: "auto" }}>
              {n.code}
            </span>
          ) : null}
        </label>
        {n.children.length > 0 ? renderNodes(n.children, depth + 1) : null}
      </div>
    ));

  return (
    <div className="modal-backdrop" role="dialog" aria-modal="true">
      <div className="modal">
        <div className="mhead">
          <Icon name="vendor" size={20} />
          <h3>SWEC categories — {vendorName}</h3>
        </div>
        <div className="mbody">
          <div className="field">
            <input
              type="text"
              placeholder="Search category, e.g. pump, valve, switchgear"
              value={kw}
              onChange={(e) => setKw(e.target.value)}
            />
          </div>
          <div className="swtree">{swec ? renderNodes(swec.tree, 0) : <Spinner />}</div>
          <div style={{ marginTop: 12 }}>
            <div className="hint" style={{ fontWeight: 700, marginBottom: 6 }}>
              Selected ({sel.size})
            </div>
            <div>
              {[...sel].map((c) => (
                <span
                  key={c}
                  className="swchip"
                  title={swec?.path(c)}
                  style={{ cursor: "pointer" }}
                  onClick={() => toggle(c)}
                >
                  {swec?.label(c) ?? c} ✕
                </span>
              ))}
              {sel.size === 0 ? <span className="hint">None selected — tick categories above.</span> : null}
            </div>
          </div>
        </div>
        <div className="mfoot">
          <button type="button" className="btn btn-out" onClick={onCancel} disabled={busy}>
            Cancel
          </button>
          <button
            type="button"
            className="btn btn-pri"
            disabled={busy}
            onClick={() => onSave([...sel])}
          >
            <Icon name="check" size={15} /> Save categories
          </button>
        </div>
      </div>
    </div>
  );
}
