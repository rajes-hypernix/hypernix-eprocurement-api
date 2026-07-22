import { useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { searchVendors } from "@/api/suppliers";
import {
  inviteVendor,
  releaseRfq,
  updateRfqDraft,
  type FormItemDto,
  type RfqDetailDto,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Notice } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";

type DraftItem = FormItemDto & { key: number };
let keySeq = 1;

export function RfqDraftEditPage({
  rfq,
  onBack,
  onReleased,
}: {
  rfq: RfqDetailDto;
  onBack: () => void;
  onReleased: () => void;
}) {
  const qc = useQueryClient();
  const [title, setTitle] = useState(rfq.title);
  const [opensUtc, setOpensUtc] = useState(rfq.opensUtc?.slice(0, 16) ?? "");
  const [closesUtc, setClosesUtc] = useState(rfq.closesUtc?.slice(0, 16) ?? "");
  const [technicalEvaluators, setTechnicalEvaluators] = useState(rfq.technicalEvaluatorIds.join("\n"));
  const [commercialEvaluators, setCommercialEvaluators] = useState(rfq.commercialEvaluatorIds.join("\n"));
  const [items, setItems] = useState<DraftItem[]>(
    rfq.formItems.map((f) => ({ ...f, key: keySeq++ })),
  );
  const [vendorSearch, setVendorSearch] = useState("");
  const [err, setErr] = useState<string | null>(null);
  const [releasing, setReleasing] = useState(false);

  const refresh = () => void qc.invalidateQueries({ queryKey: ["rfq", rfq.id] });
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const { data: vendorResults } = useQuery({
    queryKey: ["vendor-search", vendorSearch],
    queryFn: () => searchVendors({ search: vendorSearch || undefined, pageSize: 10 }),
    enabled: vendorSearch.trim().length > 1,
  });

  const invitedVendorIds = useMemo(() => new Set(rfq.invitations.map((i) => i.vendorId)), [rfq.invitations]);

  const addItem = (group: "Technical" | "Commercial") =>
    setItems((xs) => [
      ...xs,
      {
        key: keySeq++,
        kind: "question",
        group,
        section: "General",
        label: "",
        type: "text",
        required: true,
        configJson: null,
        help: null,
        order: xs.length,
      },
    ]);
  const removeItem = (key: number) => setItems((xs) => xs.filter((i) => i.key !== key));
  const updateItem = (key: number, patch: Partial<DraftItem>) =>
    setItems((xs) => xs.map((i) => (i.key === key ? { ...i, ...patch } : i)));
  const moveItem = (key: number, dir: -1 | 1) =>
    setItems((xs) => {
      const idx = xs.findIndex((i) => i.key === key);
      const swapWith = idx + dir;
      if (idx < 0 || swapWith < 0 || swapWith >= xs.length) return xs;
      const copy = [...xs];
      [copy[idx], copy[swapWith]] = [copy[swapWith]!, copy[idx]!];
      return copy.map((i, order) => ({ ...i, order }));
    });

  const save = useMutation({
    mutationFn: () =>
      updateRfqDraft(rfq.id, {
        title,
        envelope: rfq.envelope,
        currency: rfq.currency,
        opensUtc: opensUtc || null,
        closesUtc: closesUtc || null,
        lines: rfq.lines.map((l) => ({ ...l, sourcePrLineIds: l.sourcePrLineIds })),
        formItems: items.map(({ key: _key, ...i }) => i),
        technicalSections: rfq.technicalSections,
        commercialSections: rfq.commercialSections,
        technicalEvaluatorIds: technicalEvaluators.split("\n").map((s) => s.trim()).filter(Boolean),
        commercialEvaluatorIds: commercialEvaluators.split("\n").map((s) => s.trim()).filter(Boolean),
      }),
    onSuccess: refresh,
    onError: onErr,
  });

  const invite = useMutation({
    mutationFn: (vendorId: string) => inviteVendor(rfq.id, vendorId),
    onSuccess: refresh,
    onError: onErr,
  });

  const release = useMutation({
    mutationFn: () => releaseRfq(rfq.id),
    onSuccess: onReleased,
    onError: (e: Error) => {
      setReleasing(false);
      onErr(e);
    },
  });

  const technicalItems = items.filter((i) => i.group === "Technical");
  const commercialItems = items.filter((i) => i.group === "Commercial");

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          RFQs
        </button>{" "}
        <Icon name="chev" size={12} /> {rfq.code} (draft)
      </div>
      <div className="pagehead">
        <div>
          <h1>{rfq.code} · Draft</h1>
          <p>Complete settings, questions, and vendors, then release to open the bid window.</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Settings</h3>
        </div>
        <div className="cbody">
          <div className="grid g2">
            <div className="field">
              <label>Title</label>
              <input value={title} onChange={(e) => setTitle(e.target.value)} />
            </div>
            <div className="field">
              <label>Envelope</label>
              <input value={rfq.envelope} disabled />
            </div>
            <div className="field">
              <label>Bid opens</label>
              <input type="datetime-local" value={opensUtc} onChange={(e) => setOpensUtc(e.target.value)} />
            </div>
            <div className="field">
              <label>Bid closes</label>
              <input type="datetime-local" value={closesUtc} onChange={(e) => setClosesUtc(e.target.value)} />
            </div>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Lines</h3>
          <span className="sub">· {rfq.lines.length} line(s) — locked at build time</span>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Item</th>
                <th>Description</th>
                <th className="amt">Qty</th>
                <th>UoM</th>
              </tr>
            </thead>
            <tbody>
              {rfq.lines.map((l) => (
                <tr key={l.lineCode}>
                  <td>{l.lineCode}</td>
                  <td>{l.itemCode}</td>
                  <td>{l.description}</td>
                  <td className="amt">{l.qty}</td>
                  <td>{l.uom}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {rfq.envelope === "Dual" ? (
        <div className="card" style={{ marginTop: 14 }}>
          <div className="chead">
            <h3>Evaluators</h3>
          </div>
          <div className="cbody">
            <div className="grid g2">
              <div className="field" style={{ marginBottom: 0 }}>
                <label>Technical evaluators (one user id per line)</label>
                <textarea rows={3} value={technicalEvaluators} onChange={(e) => setTechnicalEvaluators(e.target.value)} />
              </div>
              <div className="field" style={{ marginBottom: 0 }}>
                <label>Commercial evaluators (one user id per line)</label>
                <textarea rows={3} value={commercialEvaluators} onChange={(e) => setCommercialEvaluators(e.target.value)} />
              </div>
            </div>
          </div>
        </div>
      ) : null}

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Questionnaire</h3>
        </div>
        <div className="cbody">
          {(["Technical", "Commercial"] as const).map((group) => (
            <div key={group} style={{ marginBottom: 16 }}>
              <div style={{ display: "flex", alignItems: "center", gap: 8, marginBottom: 8 }}>
                <strong style={{ fontSize: 13 }}>{group}</strong>
                <div className="spacer" style={{ flex: 1 }} />
                <button type="button" className="btn btn-out btn-sm" onClick={() => addItem(group)}>
                  <Icon name="plus" size={13} /> Add item
                </button>
              </div>
              {(group === "Technical" ? technicalItems : commercialItems).map((it, i, arr) => (
                <div className="pickrow" key={it.key}>
                  <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm"
                      disabled={i === 0}
                      onClick={() => moveItem(it.key, -1)}
                    >
                      ▲
                    </button>
                    <button
                      type="button"
                      className="btn btn-ghost btn-sm"
                      disabled={i === arr.length - 1}
                      onClick={() => moveItem(it.key, 1)}
                    >
                      ▼
                    </button>
                  </div>
                  <input
                    style={{ flex: 2 }}
                    value={it.label}
                    placeholder="Question label"
                    onChange={(e) => updateItem(it.key, { label: e.target.value })}
                  />
                  <select value={it.type} onChange={(e) => updateItem(it.key, { type: e.target.value })}>
                    <option value="text">Short text</option>
                    <option value="longtext">Long text</option>
                    <option value="number">Number</option>
                    <option value="date">Date</option>
                    <option value="select">Single select</option>
                    <option value="file">File attachment</option>
                  </select>
                  <label className="hint" style={{ display: "flex", alignItems: "center", gap: 4 }}>
                    <input
                      type="checkbox"
                      checked={it.required}
                      onChange={(e) => updateItem(it.key, { required: e.target.checked })}
                    />
                    Required
                  </label>
                  <button type="button" className="btn btn-ghost btn-sm" onClick={() => removeItem(it.key)}>
                    <Icon name="x" size={13} />
                  </button>
                </div>
              ))}
              {(group === "Technical" ? technicalItems : commercialItems).length === 0 ? (
                <p className="hint" style={{ margin: 0 }}>
                  No {group.toLowerCase()} questions yet.
                </p>
              ) : null}
            </div>
          ))}
          <button type="button" className="btn btn-out btn-sm" disabled={save.isPending} onClick={() => save.mutate()}>
            Save settings &amp; questions
          </button>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Invited vendors</h3>
          <span className="sub">· {rfq.invitations.length} invited</span>
        </div>
        <div className="cbody">
          <div className="field" style={{ maxWidth: 360 }}>
            <label>Search vendors to invite</label>
            <input value={vendorSearch} onChange={(e) => setVendorSearch(e.target.value)} placeholder="name or code" />
          </div>
          {vendorResults?.items.map((v) => (
            <div className="pickrow" key={v.id}>
              <div style={{ flex: 1 }}>
                <div style={{ fontWeight: 600, fontSize: 13 }}>{v.name}</div>
                <div className="hint">{v.code}</div>
              </div>
              {invitedVendorIds.has(v.id) ? (
                <span className="badge b-grey">Invited</span>
              ) : (
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={invite.isPending}
                  onClick={() => invite.mutate(v.id)}
                >
                  Invite
                </button>
              )}
            </div>
          ))}

          {rfq.invitations.length > 0 ? (
            <table style={{ marginTop: 12 }}>
              <thead>
                <tr>
                  <th>Vendor</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {rfq.invitations.map((i) => (
                  <tr key={i.id}>
                    <td>
                      {i.vendorName} <span className="hint">{i.vendorCode}</span>
                    </td>
                    <td>
                      <span className="badge b-grey">{i.status}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          ) : null}
        </div>
      </div>

      <div className="actionbar" style={{ marginTop: 14 }}>
        <span className="hint">Releasing locks line items and questions. Vendor invites and clarifications still work after.</span>
        <div className="spacer" style={{ flex: 1 }} />
        <button
          type="button"
          className="btn btn-pri"
          disabled={rfq.invitations.length === 0 || !closesUtc}
          onClick={() => setReleasing(true)}
        >
          Release to {rfq.invitations.length} vendor(s) <Icon name="chev" size={14} />
        </button>
      </div>

      {releasing ? (
        <ConfirmModal
          title="Release RFQ"
          icon="rfq"
          body="Line items and questions lock once released. Vendors will be notified. This cannot be undone."
          confirmLabel="Release"
          busy={release.isPending}
          onCancel={() => setReleasing(false)}
          onConfirm={() => release.mutate()}
        />
      ) : null}
    </>
  );
}
