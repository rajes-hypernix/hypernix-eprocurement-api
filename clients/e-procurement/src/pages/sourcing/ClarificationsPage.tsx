import { useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useAuth } from "@/auth/use-auth";
import { searchVendors } from "@/api/suppliers";
import {
  listClarificationThreads,
  listMyInvitations,
  listRfqs,
  getClarificationThread,
  sendClarification,
  type ClarificationThreadDto,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { EmptyState, Modal, Notice, Spinner } from "@/components/ui";
import { dateTimeMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

export function ClarificationsPage() {
  const qc = useQueryClient();
  const { isVendor, user } = useAuth();
  const [active, setActive] = useState<ClarificationThreadDto | null>(null);
  const [composing, setComposing] = useState(false);
  const [reply, setReply] = useState("");
  const [publish, setPublish] = useState(false);
  const [err, setErr] = useState<string | null>(null);

  const [newScope, setNewScope] = useState("general");
  const [newVendorId, setNewVendorId] = useState("");
  const [newVendorSearch, setNewVendorSearch] = useState("");
  const [newBody, setNewBody] = useState("");
  const [newPublish, setNewPublish] = useState(false);

  const [searchParams] = useSearchParams();
  const prefillVendorId = searchParams.get("vendorId");
  const prefillConsumed = useRef(false);

  const { data: threads, isPending } = useQuery({ queryKey: ["clar-threads"], queryFn: listClarificationThreads });
  const { data: messages } = useQuery({
    queryKey: ["clar-thread", active?.scope, active?.vendorId],
    queryFn: () => getClarificationThread(active!.scope, active!.vendorId),
    enabled: active !== null,
  });

  const { data: myRfqs } = useQuery({ queryKey: ["my-invitations"], queryFn: listMyInvitations, enabled: isVendor });
  const { data: allRfqs } = useQuery({ queryKey: ["rfqs"], queryFn: listRfqs, enabled: !isVendor });
  const { data: vendorResults } = useQuery({
    queryKey: ["vendor-search", newVendorSearch],
    queryFn: () => searchVendors({ search: newVendorSearch || undefined, pageSize: 8 }),
    enabled: !isVendor && composing && newVendorSearch.trim().length > 1,
  });

  const refreshThread = () => {
    void qc.invalidateQueries({ queryKey: ["clar-thread", active?.scope, active?.vendorId] });
    void qc.invalidateQueries({ queryKey: ["clar-threads"] });
  };
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const send = useMutation({
    mutationFn: () => sendClarification(active!.scope, active!.vendorId, reply, isVendor ? false : publish),
    onSuccess: () => {
      setReply("");
      setPublish(false);
      refreshThread();
    },
    onError: onErr,
  });

  const startNew = useMutation({
    mutationFn: () => {
      const vendorId = isVendor ? user!.vendorId! : newVendorId || null;
      const broadcast = Boolean(newPublish && newScope !== "general");
      return sendClarification(newScope, broadcast ? null : vendorId, newBody, newPublish);
    },
    onSuccess: () => {
      setComposing(false);
      setNewBody("");
      setNewVendorId("");
      setNewScope("general");
      setNewPublish(false);
      void qc.invalidateQueries({ queryKey: ["clar-threads"] });
    },
    onError: onErr,
  });

  useEffect(() => {
    if (prefillConsumed.current || !prefillVendorId || isVendor || threads === undefined) return;
    prefillConsumed.current = true;
    const hit = threads.find((t) => t.vendorId === prefillVendorId);
    if (hit) {
      setActive(hit);
      return;
    }
    setNewVendorId(prefillVendorId);
    setNewScope("general");
    setComposing(true);
  }, [prefillVendorId, isVendor, threads]);

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Clarifications</h1>
          <p>RFQ Q&amp;A threads, plus general inquiries.</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri btn-sm" onClick={() => setComposing(true)}>
          <Icon name="plus" size={14} /> New clarification
        </button>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="grid g2" style={{ alignItems: "start" }}>
        <div className="card">
          <div className="chead">
            <h3>Threads</h3>
          </div>
          <div className="cbody" style={{ padding: 0 }}>
            {isPending ? (
              <Spinner label="Loading…" />
            ) : (threads ?? []).length === 0 ? (
              <EmptyState>No clarifications yet.</EmptyState>
            ) : (
              (threads ?? []).map((t) => (
                <div
                  key={`${t.scope}|${t.vendorId}`}
                  className={`pickrow${active?.scope === t.scope && active?.vendorId === t.vendorId ? " on" : ""}`}
                  style={{ cursor: "pointer" }}
                  onClick={() => setActive(t)}
                >
                  <div style={{ flex: 1 }}>
                    <div style={{ fontWeight: 700, fontSize: 13 }}>
                      {isVendor ? "Procurement" : t.vendorName ?? t.vendorCode}
                    </div>
                    <div className="hint">{t.scope === "general" ? "General inquiry" : t.scope}</div>
                  </div>
                  {t.hasUnread ? <span className="badge b-blue">New</span> : null}
                </div>
              ))
            )}
          </div>
        </div>

        <div className="card">
          <div className="chead">
            <h3>{active ? (active.scope === "general" ? "General inquiry" : active.scope) : "Select a thread"}</h3>
          </div>
          <div className="cbody">
            {!active ? (
              <p className="hint" style={{ margin: 0 }}>
                Pick a thread on the left, or start a new clarification.
              </p>
            ) : (
              <>
                <div style={{ maxHeight: 360, overflowY: "auto", marginBottom: 12 }}>
                  {(messages ?? []).map((m) => {
                    const mine = isVendor ? m.senderKind === "Vendor" : m.senderKind === "Buyer";
                    return (
                      <div key={m.id} style={{ textAlign: mine ? "right" : "left", marginBottom: 10 }}>
                        <div
                          className="card"
                          style={{
                            display: "inline-block",
                            maxWidth: "80%",
                            background: mine ? "var(--soft)" : undefined,
                          }}
                        >
                          <div className="cbody" style={{ padding: "8px 12px" }}>
                            <div style={{ fontSize: 13 }}>{m.body}</div>
                            <div className="hint" style={{ marginTop: 4 }}>
                              {m.senderName} · {dateTimeMY(m.createdUtc)}
                              {m.published ? " · Published to all bidders" : ""}
                            </div>
                          </div>
                        </div>
                      </div>
                    );
                  })}
                  {(messages ?? []).length === 0 ? (
                    <p className="hint" style={{ margin: 0 }}>
                      No messages yet.
                    </p>
                  ) : null}
                </div>
                <div className="field" style={{ marginBottom: 8 }}>
                  <textarea rows={2} value={reply} onChange={(e) => setReply(e.target.value)} placeholder="Write a reply…" />
                </div>
                {!isVendor && active.scope !== "general" ? (
                  <label className="hint" style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 8 }}>
                    <input type="checkbox" checked={publish} onChange={(e) => setPublish(e.target.checked)} />
                    Share this reply with all bidders (anonymised)
                  </label>
                ) : null}
                <button type="button" className="btn btn-pri btn-sm" disabled={!reply.trim() || send.isPending} onClick={() => send.mutate()}>
                  Send
                </button>
              </>
            )}
          </div>
        </div>
      </div>

      {composing ? (
        <Modal
          title="New clarification"
          icon="msg"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setComposing(false)}>
                Cancel
              </button>
              <button
                type="button"
                className="btn btn-pri"
                disabled={!newBody.trim() || (!isVendor && !newPublish && !newVendorId) || startNew.isPending}
                onClick={() => startNew.mutate()}
              >
                Send
              </button>
            </>
          }
        >
          <div className="field">
            <label>Topic</label>
            <select value={newScope} onChange={(e) => setNewScope(e.target.value)}>
              <option value="general">General inquiry (not tied to an RFQ)</option>
              {isVendor
                ? (myRfqs ?? []).map((r) => (
                    <option key={r.rfqId} value={r.rfqCode}>
                      {r.rfqCode} — {r.title}
                    </option>
                  ))
                : (allRfqs ?? []).map((r) => (
                    <option key={r.id} value={r.code}>
                      {r.code} — {r.title}
                    </option>
                  ))}
            </select>
          </div>
          {!isVendor && newScope !== "general" ? (
            <label className="hint" style={{ display: "flex", alignItems: "center", gap: 6, marginBottom: 10 }}>
              <input type="checkbox" checked={newPublish} onChange={(e) => setNewPublish(e.target.checked)} />
              Publish to all bidders on this RFQ
            </label>
          ) : null}
          {!isVendor && !newPublish ? (
            <div className="field">
              <label>Vendor</label>
              {newVendorId && newVendorSearch.trim().length <= 1 ? (
                <p className="hint" style={{ margin: "0 0 8px" }}>
                  Vendor selected from statement. Search to pick a different one.
                </p>
              ) : null}
              <input value={newVendorSearch} onChange={(e) => setNewVendorSearch(e.target.value)} placeholder="Search vendor…" />
              {vendorResults?.items.map((v) => (
                <div
                  key={v.id}
                  className="pickrow"
                  style={{ cursor: "pointer", background: newVendorId === v.id ? "var(--soft)" : undefined }}
                  onClick={() => setNewVendorId(v.id)}
                >
                  {v.name} <span className="hint">{v.code}</span>
                </div>
              ))}
            </div>
          ) : null}
          <div className="field" style={{ marginBottom: 0 }}>
            <label>Message</label>
            <textarea rows={3} value={newBody} onChange={(e) => setNewBody(e.target.value)} />
          </div>
        </Modal>
      ) : null}
    </>
  );
}
