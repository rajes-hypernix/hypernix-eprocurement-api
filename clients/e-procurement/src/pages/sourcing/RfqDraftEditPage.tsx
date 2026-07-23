import { useMemo, useState } from "react";
import { useMutation, useQueries, useQuery, useQueryClient } from "@tanstack/react-query";
import { searchVendors } from "@/api/suppliers";
import { searchUsers, getUserById } from "@/api/identity";
import { listFormTemplates, getFormTemplate } from "@/api/platform";
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

type Draft = {
  title: string;
  envelope: string;
  currency: string;
  opensUtc: string;
  closesUtc: string;
  lines: DraftLine[];
  items: DraftItem[];
  technicalSections: string[];
  commercialSections: string[];
  techEvals: string[];
  commEvals: string[];
};

type DraftLine = RfqDetailDto["lines"][number];

const isoToLocal = (iso: string | null | undefined) => (iso ? iso.slice(0, 16) : "");
const localToIso = (v: string) => (v ? new Date(v).toISOString() : null);

function fromRfq(r: RfqDetailDto): Draft {
  return {
    title: r.title ?? "",
    envelope: r.envelope,
    currency: r.currency,
    opensUtc: isoToLocal(r.opensUtc),
    closesUtc: isoToLocal(r.closesUtc),
    lines: [...r.lines],
    items: r.formItems.map((f) => ({ ...f, key: keySeq++ })),
    technicalSections: [...r.technicalSections],
    commercialSections: [...r.commercialSections],
    techEvals: [...r.technicalEvaluatorIds],
    commEvals: [...r.commercialEvaluatorIds],
  };
}

/**
 * Phase 3 — multi-step RFQ draft wizard, ported from tmp-old-port/RfqBuilder.tsx onto the new
 * sourcing.ts API surface. Released RFQs never reach this component (RfqDetailPage dispatches
 * Draft -> here, everything else -> RfqDetailHubPage), so there's no "released" branch to handle.
 */
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
  const [draft, setDraft] = useState<Draft>(() => fromRfq(rfq));
  const [step, setStep] = useState(0);
  const [notice, setNotice] = useState("");
  const [showRelease, setShowRelease] = useState(false);
  const [vendorSearch, setVendorSearch] = useState("");
  const [userSearch, setUserSearch] = useState("");
  const [templateGroup, setTemplateGroup] = useState<"Technical" | "Commercial">("Technical");
  const [templateId, setTemplateId] = useState("");

  const patch = (p: Partial<Draft>) => setDraft((d) => ({ ...d, ...p }));
  const refresh = () => void qc.invalidateQueries({ queryKey: ["rfq", rfq.id] });
  const onErr = (e: Error) => setNotice(e instanceof ApiRequestError ? e.message : e.message);

  const { data: templates = [] } = useQuery({ queryKey: ["form-templates"], queryFn: () => listFormTemplates() });
  const { data: vendorResults } = useQuery({
    queryKey: ["vendor-search", vendorSearch],
    queryFn: () => searchVendors({ search: vendorSearch || undefined, pageSize: 10 }),
    enabled: vendorSearch.trim().length > 1,
  });
  const { data: userResults } = useQuery({
    queryKey: ["user-search", userSearch],
    queryFn: () => searchUsers({ search: userSearch || undefined, pageSize: 10 }),
    enabled: userSearch.trim().length > 1,
  });

  const invitedVendorIds = useMemo(() => new Set(rfq.invitations.map((i) => i.vendorId)), [rfq.invitations]);
  const evaluatorIds = useMemo(() => [...new Set([...draft.techEvals, ...draft.commEvals])], [draft.techEvals, draft.commEvals]);
  const evaluatorResults = useQueries({
    queries: evaluatorIds.map((id) => ({ queryKey: ["user", id], queryFn: () => getUserById(id) })),
  });
  const evaluatorName = (id: string) => {
    const i = evaluatorIds.indexOf(id);
    const u = i >= 0 ? evaluatorResults[i]?.data : undefined;
    return u ? `${u.firstName ?? ""} ${u.lastName ?? ""}`.trim() || u.userName || id : id;
  };

  const toRequest = (cur: Draft) => ({
    title: cur.title,
    envelope: cur.envelope,
    currency: cur.currency,
    opensUtc: localToIso(cur.opensUtc),
    closesUtc: localToIso(cur.closesUtc),
    lines: cur.lines,
    formItems: cur.items.map(({ key: _key, ...i }) => i),
    technicalSections: cur.technicalSections,
    commercialSections: cur.commercialSections,
    technicalEvaluatorIds: cur.techEvals,
    commercialEvaluatorIds: cur.commEvals,
  });

  const save = useMutation({
    mutationFn: (cur: Draft) => updateRfqDraft(rfq.id, toRequest(cur)),
    onSuccess: () => {
      refresh();
      setNotice("Draft saved");
    },
    onError: onErr,
  });
  const invite = useMutation({
    mutationFn: (vendorId: string) => inviteVendor(rfq.id, vendorId),
    onSuccess: () => {
      refresh();
      setVendorSearch("");
    },
    onError: onErr,
  });
  const release = useMutation({
    mutationFn: async (cur: Draft) => {
      await updateRfqDraft(rfq.id, toRequest(cur));
      return releaseRfq(rfq.id);
    },
    onSuccess: onReleased,
    onError: (e: Error) => {
      setShowRelease(false);
      onErr(e);
    },
  });
  const appendTemplate = useMutation({
    mutationFn: (id: string) => getFormTemplate(id),
    onSuccess: (tpl) => {
      if (!tpl) return;
      setDraft((d) => ({
        ...d,
        items: [
          ...d.items,
          ...tpl.questions.map((q) => ({
            key: keySeq++,
            kind: "question",
            group: templateGroup,
            section: "General",
            label: q.label,
            type: q.type,
            required: q.required,
            configJson: q.configJson ?? null,
            help: q.help ?? null,
            order: d.items.length,
          })),
        ],
      }));
      setTemplateId("");
    },
    onError: onErr,
  });

  const steps =
    draft.envelope === "Dual"
      ? ["Items", "Settings", "Questions", "Vendors", "Evaluators", "Review"]
      : ["Items", "Settings", "Questions", "Vendors", "Review"];
  const last = steps.length - 1;
  const stepName = steps[step];
  const settingsIdx = steps.indexOf("Settings");
  const settingsComplete = !!(draft.title && draft.opensUtc && draft.closesUtc);

  const validateStep = () => {
    if (stepName === "Settings" && !settingsComplete) {
      setNotice("RFQ title, bid open date/time and bid close date/time are required.");
      return false;
    }
    setNotice("");
    return true;
  };
  const goToStep = (i: number) => {
    if (i > settingsIdx && !settingsComplete) {
      setNotice("Complete the RFQ title and bid dates in Settings first.");
      setStep(settingsIdx);
      return;
    }
    setNotice("");
    setStep(i);
  };

  const addItem = (group: "Technical" | "Commercial") =>
    setDraft((d) => ({
      ...d,
      items: [
        ...d.items,
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
          order: d.items.length,
        },
      ],
    }));
  const removeItem = (key: number) => setDraft((d) => ({ ...d, items: d.items.filter((i) => i.key !== key) }));
  const updateItem = (key: number, p: Partial<DraftItem>) =>
    setDraft((d) => ({ ...d, items: d.items.map((i) => (i.key === key ? { ...i, ...p } : i)) }));
  const moveItem = (key: number, dir: -1 | 1) =>
    setDraft((d) => {
      const idx = d.items.findIndex((i) => i.key === key);
      const swapWith = idx + dir;
      if (idx < 0 || swapWith < 0 || swapWith >= d.items.length) return d;
      const copy = [...d.items];
      [copy[idx], copy[swapWith]] = [copy[swapWith]!, copy[idx]!];
      return { ...d, items: copy.map((i, order) => ({ ...i, order })) };
    });
  const removeLine = (lineCode: string) => setDraft((d) => ({ ...d, lines: d.lines.filter((l) => l.lineCode !== lineCode) }));

  const toggle = (arr: string[], v: string) => (arr.includes(v) ? arr.filter((x) => x !== v) : [...arr, v]);

  const technicalItems = draft.items.filter((i) => i.group === "Technical");
  const commercialItems = draft.items.filter((i) => i.group === "Commercial");
  const prRefs = [...new Set(draft.lines.map((l) => l.prRef).filter((v): v is string => !!v))];

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          RFQs
        </button>{" "}
        <Icon name="chev" size={13} /> <span>{rfq.code}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>Create RFQ</h1>
          <p>
            {rfq.code} · from {prRefs.join(", ") || "—"}
          </p>
        </div>
        <div className="spacer" />
        <div className="actbar">
          {step > 0 ? (
            <button type="button" className="btn btn-out" onClick={() => setStep((s) => Math.max(s - 1, 0))}>
              <Icon name="back" size={15} /> Back
            </button>
          ) : null}
          <button type="button" className="btn btn-out" disabled={save.isPending} onClick={() => save.mutate(draft)}>
            <Icon name="doc" size={15} /> Save as draft
          </button>
          {step < last ? (
            <button
              type="button"
              className="btn btn-pri"
              onClick={() => {
                if (validateStep()) setStep((s) => Math.min(s + 1, last));
              }}
            >
              Continue <Icon name="chev" size={15} />
            </button>
          ) : (
            <button
              type="button"
              className="btn btn-pri"
              onClick={() => (rfq.invitations.length ? setShowRelease(true) : setNotice("Invite at least one vendor before releasing"))}
            >
              <Icon name="send" size={15} /> Release to {rfq.invitations.length} vendor{rfq.invitations.length !== 1 ? "s" : ""}
            </button>
          )}
        </div>
      </div>

      <div className="steps">
        {steps.map((s, i) => (
          <span key={s} style={{ display: "contents" }}>
            <button type="button" className={`step${i === step ? " on" : i < step ? " done" : ""}`} onClick={() => goToStep(i)}>
              <span className="n">{i < step ? "✓" : i + 1}</span>
              {s}
            </button>
            {i < steps.length - 1 ? <span className="sep" /> : null}
          </span>
        ))}
      </div>

      {notice ? <Notice>{notice}</Notice> : null}

      {stepName === "Items" ? (
        <div className="card">
          <div className="chead">
            <h3>RFQ Line Items</h3>
            <div className="spacer" />
            <span className="hint" style={{ marginRight: 12 }}>
              {draft.lines.length} {draft.lines.length === 1 ? "line" : "lines"} from {prRefs.length} PR{prRefs.length !== 1 ? "s" : ""}
            </span>
          </div>
          <table className="rfqlt">
            <thead>
              <tr>
                <th>PR #</th>
                <th>Code</th>
                <th>Item</th>
                <th>Description</th>
                <th className="amt">Qty</th>
                <th>UoM</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {draft.lines.map((l) => (
                <tr key={l.lineCode}>
                  <td>
                    <span className="prtag">{l.prRef ?? "—"}</span>
                  </td>
                  <td>{l.lineCode}</td>
                  <td>{l.itemCode}</td>
                  <td>{l.description}</td>
                  <td className="amt">{l.qty}</td>
                  <td>{l.uom}</td>
                  <td className="amt">
                    <button type="button" className="btn btn-ghost btn-sm" title="Remove line" onClick={() => removeLine(l.lineCode)}>
                      <Icon name="x" size={15} />
                    </button>
                  </td>
                </tr>
              ))}
              {draft.lines.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ padding: 22, textAlign: "center", color: "var(--muted)" }}>
                    No lines on this RFQ.
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        </div>
      ) : null}

      {stepName === "Settings" ? (
        <div className="card">
          <div className="cbody">
            <div className="field">
              <label>RFQ title *</label>
              <input
                type="text"
                required
                value={draft.title}
                placeholder="e.g. Pump &amp; VFD Package — Facilities Upgrade"
                onChange={(e) => patch({ title: e.target.value })}
                style={!draft.title ? { borderColor: "var(--amber)" } : undefined}
              />
            </div>
            <div className="grid g2">
              <div className="field">
                <label>Bid opens *</label>
                <input
                  type="datetime-local"
                  required
                  value={draft.opensUtc}
                  onChange={(e) => patch({ opensUtc: e.target.value })}
                  style={!draft.opensUtc ? { borderColor: "var(--amber)" } : undefined}
                />
              </div>
              <div className="field">
                <label>Bid closes *</label>
                <input
                  type="datetime-local"
                  required
                  value={draft.closesUtc}
                  onChange={(e) => patch({ closesUtc: e.target.value })}
                  style={!draft.closesUtc ? { borderColor: "var(--amber)" } : undefined}
                />
              </div>
            </div>
            <div className="grid g2">
              <div className="field" style={{ maxWidth: 240 }}>
                <label>Currency</label>
                <select value={draft.currency} onChange={(e) => patch({ currency: e.target.value })}>
                  {["MYR", "USD", "SGD", "EUR", "GBP"].map((c) => (
                    <option key={c}>{c}</option>
                  ))}
                </select>
              </div>
              <div className="field" style={{ maxWidth: 240 }}>
                <label>Envelope type</label>
                <input value={draft.envelope} disabled />
                <p className="hint" style={{ margin: "6px 0 0" }}>
                  Set when the RFQ was built — locked for this draft.
                </p>
              </div>
            </div>
          </div>
        </div>
      ) : null}

      {stepName === "Questions" ? (
        <>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="cbody" style={{ display: "flex", gap: 12, alignItems: "center", flexWrap: "wrap" }}>
              <label className="hint" style={{ fontWeight: 700 }}>
                Append from library
              </label>
              <select value={templateGroup} onChange={(e) => setTemplateGroup(e.target.value as "Technical" | "Commercial")}>
                <option value="Technical">Technical</option>
                <option value="Commercial">Commercial</option>
              </select>
              <select value={templateId} onChange={(e) => setTemplateId(e.target.value)} style={{ maxWidth: 320 }}>
                <option value="">Choose a form template…</option>
                {templates.map((t) => (
                  <option key={t.id} value={t.id}>
                    {t.name} · {t.questionCount} question(s)
                  </option>
                ))}
              </select>
              <button
                type="button"
                className="btn btn-out btn-sm"
                disabled={!templateId || appendTemplate.isPending}
                onClick={() => appendTemplate.mutate(templateId)}
              >
                <Icon name="plus" size={13} /> Append questions
              </button>
              <span className="hint">Adds every question in the template to the {templateGroup.toLowerCase()} group below.</span>
            </div>
          </div>
          {(["Technical", "Commercial"] as const).map((group) => (
            <div className="card" style={{ marginBottom: 14 }} key={group}>
              <div className="chead">
                <h3>{group}</h3>
                <div className="spacer" />
                <button type="button" className="btn btn-out btn-sm" onClick={() => addItem(group)}>
                  <Icon name="plus" size={13} /> Add item
                </button>
              </div>
              <div className="cbody">
                {(group === "Technical" ? technicalItems : commercialItems).map((it, i, arr) => (
                  <div className="pickrow" key={it.key}>
                    <div style={{ display: "flex", flexDirection: "column", gap: 2 }}>
                      <button type="button" className="btn btn-ghost btn-sm" disabled={i === 0} onClick={() => moveItem(it.key, -1)}>
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
                      <input type="checkbox" checked={it.required} onChange={(e) => updateItem(it.key, { required: e.target.checked })} />
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
            </div>
          ))}
        </>
      ) : null}

      {stepName === "Vendors" ? (
        <div className="card">
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
                  <button type="button" className="btn btn-out btn-sm" disabled={invite.isPending} onClick={() => invite.mutate(v.id)}>
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
      ) : null}

      {stepName === "Evaluators" ? (
        <>
          <div className="card" style={{ marginBottom: 14 }}>
            <div className="chead">
              <h3>Evaluators</h3>
            </div>
            <div className="cbody">
              <p className="hint" style={{ marginTop: 0 }}>
                Search users, then flag them as technical and/or commercial evaluators for this RFQ's sealed envelopes.
              </p>
              <div className="field" style={{ maxWidth: 360 }}>
                <label>Search users</label>
                <input value={userSearch} onChange={(e) => setUserSearch(e.target.value)} placeholder="name, username or email" />
              </div>
              {userResults?.items.map((u) => (
                <div className="pickrow" key={u.id}>
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 600, fontSize: 13 }}>
                      {u.firstName} {u.lastName}
                    </div>
                    <div className="hint">{u.userName ?? u.email}</div>
                  </div>
                  <label className="hint" style={{ display: "flex", alignItems: "center", gap: 4 }}>
                    <input
                      type="checkbox"
                      checked={draft.techEvals.includes(u.id ?? "")}
                      onChange={() => u.id && patch({ techEvals: toggle(draft.techEvals, u.id) })}
                    />
                    Technical
                  </label>
                  <label className="hint" style={{ display: "flex", alignItems: "center", gap: 4 }}>
                    <input
                      type="checkbox"
                      checked={draft.commEvals.includes(u.id ?? "")}
                      onChange={() => u.id && patch({ commEvals: toggle(draft.commEvals, u.id) })}
                    />
                    Commercial
                  </label>
                </div>
              ))}
            </div>
          </div>
          <div className="grid g2">
            <div className="card">
              <div className="chead">
                <h3>Technical envelope</h3>
                <span className="sub">· {draft.techEvals.length} evaluator(s)</span>
              </div>
              <div className="cbody">
                {draft.techEvals.length === 0 ? (
                  <p className="hint" style={{ margin: 0 }}>
                    No evaluators assigned yet.
                  </p>
                ) : (
                  draft.techEvals.map((id) => (
                    <span className="chip" key={id}>
                      {evaluatorName(id)}{" "}
                      <button type="button" className="lnk" style={{ padding: 0 }} onClick={() => patch({ techEvals: toggle(draft.techEvals, id) })}>
                        <Icon name="x" size={11} />
                      </button>
                    </span>
                  ))
                )}
              </div>
            </div>
            <div className="card">
              <div className="chead">
                <h3>Commercial envelope</h3>
                <span className="sub">· {draft.commEvals.length} evaluator(s)</span>
              </div>
              <div className="cbody">
                {draft.commEvals.length === 0 ? (
                  <p className="hint" style={{ margin: 0 }}>
                    No evaluators assigned yet.
                  </p>
                ) : (
                  draft.commEvals.map((id) => (
                    <span className="chip" key={id}>
                      {evaluatorName(id)}{" "}
                      <button type="button" className="lnk" style={{ padding: 0 }} onClick={() => patch({ commEvals: toggle(draft.commEvals, id) })}>
                        <Icon name="x" size={11} />
                      </button>
                    </span>
                  ))
                )}
              </div>
            </div>
          </div>
        </>
      ) : null}

      {stepName === "Review" ? (
        <div className="card">
          <div className="chead">
            <h3>Review &amp; send</h3>
          </div>
          <div className="cbody">
            <div className="grid g2">
              <div>
                <div className="hint">Title</div>
                <div style={{ fontWeight: 700 }}>{draft.title || "—"}</div>
              </div>
              <div>
                <div className="hint">Bid window</div>
                <div style={{ fontWeight: 700 }}>
                  {draft.opensUtc || "—"} → {draft.closesUtc || "—"}
                </div>
              </div>
              <div>
                <div className="hint">Currency</div>
                <div style={{ fontWeight: 700 }}>{draft.currency}</div>
              </div>
              <div>
                <div className="hint">Envelope</div>
                <div style={{ fontWeight: 700 }}>{draft.envelope}</div>
              </div>
              <div>
                <div className="hint">Lines / Questions</div>
                <div style={{ fontWeight: 700 }}>
                  {draft.lines.length} lines · {draft.items.length} questions
                </div>
              </div>
              <div>
                <div className="hint">Vendors</div>
                <div style={{ fontWeight: 700 }}>{rfq.invitations.length} invited</div>
              </div>
              {draft.envelope === "Dual" ? (
                <div>
                  <div className="hint">Evaluators</div>
                  <div style={{ fontWeight: 700 }}>
                    {draft.techEvals.length} technical · {draft.commEvals.length} commercial
                  </div>
                </div>
              ) : null}
            </div>
            <p className="hint" style={{ marginTop: 14 }}>
              Releasing captures the closing date as the server-side bid deadline and opens the RFQ to invited vendors.
              Line items and questions are locked after release — you can still send clarifications.
            </p>
          </div>
        </div>
      ) : null}

      {showRelease ? (
        <ConfirmModal
          icon="send"
          title="Release RFQ to Vendors"
          body={
            <>
              <p className="hint" style={{ marginTop: 0 }}>
                Once released, the invited vendors can see this RFQ and start bidding. Line items and questions are
                locked after release — you can still send clarifications.
              </p>
              <div className="locked-note">
                <b>{draft.title || "Untitled RFQ"}</b>
                <br />
                {draft.lines.length} line(s) · {draft.items.length} question(s) · {rfq.invitations.length} vendor(s) · closes{" "}
                {draft.closesUtc || "—"}
              </div>
            </>
          }
          cancelLabel="Not yet"
          confirmLabel="Release now"
          busy={release.isPending}
          onCancel={() => setShowRelease(false)}
          onConfirm={() => release.mutate(draft)}
        />
      ) : null}
    </>
  );
}
