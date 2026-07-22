import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { searchVendors } from "@/api/suppliers";
import { CustomListKeys, listCustomListItems } from "@/api/platform";
import {
  cancelRfq,
  closeRfq,
  extendRfq,
  inviteVendor,
  rescindInvitation,
  type RfqDetailDto,
  type RfqInvitationDto,
} from "@/api/sourcing";
import { Icon } from "@/components/Icon";
import { ConfirmModal, Modal, Notice } from "@/components/ui";
import { Kv, Stat } from "@/components/vendors/EntityPage";
import { SourcingStatusBadge, EnvelopeTag } from "@/components/sourcing/badges";
import { dateTimeMY } from "@/lib/format";
import { ApiRequestError } from "@/lib/api-client";

const RESCINDABLE = new Set(["Invited", "Viewed", "IntendToBid", "Declined"]);

export function RfqDetailHubPage({
  rfq,
  onBack,
  onNavigate,
}: {
  rfq: RfqDetailDto;
  onBack: () => void;
  onNavigate: (key: string) => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [inviting, setInviting] = useState(false);
  const [vendorSearch, setVendorSearch] = useState("");
  const [rescinding, setRescinding] = useState<RfqInvitationDto | null>(null);
  const [extending, setExtending] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [reasonCode, setReasonCode] = useState("");
  const [note, setNote] = useState("");
  const [newClosesUtc, setNewClosesUtc] = useState("");

  const refresh = () => void qc.invalidateQueries({ queryKey: ["rfq", rfq.id] });
  const onErr = (e: Error) => setErr(e instanceof ApiRequestError ? e.message : e.message);

  const { data: rescindReasons } = useQuery({
    queryKey: ["custom-list", CustomListKeys.rfqRescind],
    queryFn: () => listCustomListItems(CustomListKeys.rfqRescind),
    enabled: rescinding !== null,
  });
  const { data: extendReasons } = useQuery({
    queryKey: ["custom-list", CustomListKeys.rfqExtend],
    queryFn: () => listCustomListItems(CustomListKeys.rfqExtend),
    enabled: extending,
  });
  const { data: vendorResults } = useQuery({
    queryKey: ["vendor-search", vendorSearch],
    queryFn: () => searchVendors({ search: vendorSearch || undefined, pageSize: 10 }),
    enabled: inviting && vendorSearch.trim().length > 1,
  });

  const canGovern = rfq.status === "Draft" || rfq.status === "Open";
  const invitedVendorIds = new Set(rfq.invitations.map((i) => i.vendorId));
  const submittedCount = rfq.invitations.filter((i) => i.status === "BidSubmitted").length;

  const invite = useMutation({
    mutationFn: (vendorId: string) => inviteVendor(rfq.id, vendorId),
    onSuccess: () => {
      refresh();
      setVendorSearch("");
    },
    onError: onErr,
  });

  const rescind = useMutation({
    mutationFn: () => rescindInvitation(rfq.id, rescinding!.vendorId, reasonCode, note || null),
    onSuccess: () => {
      setRescinding(null);
      setReasonCode("");
      setNote("");
      refresh();
    },
    onError: onErr,
  });

  const extend = useMutation({
    mutationFn: () => extendRfq(rfq.id, new Date(newClosesUtc).toISOString(), reasonCode || null, note || null),
    onSuccess: () => {
      setExtending(false);
      setReasonCode("");
      setNote("");
      setNewClosesUtc("");
      refresh();
    },
    onError: onErr,
  });

  const close = useMutation({ mutationFn: () => closeRfq(rfq.id), onSuccess: refresh, onError: onErr });
  const cancel = useMutation({
    mutationFn: () => cancelRfq(rfq.id),
    onSuccess: () => {
      setCancelling(false);
      refresh();
    },
    onError: onErr,
  });

  const cta =
    rfq.status === "Closed" ? (
      <button type="button" className="btn btn-pri" onClick={() => onNavigate(`openings/${rfq.id}`)}>
        Open bids <Icon name="chev" size={14} />
      </button>
    ) : rfq.status === "Evaluation" ? (
      <button type="button" className="btn btn-pri" onClick={() => onNavigate(`awards/${rfq.id}`)}>
        Proceed to award <Icon name="chev" size={14} />
      </button>
    ) : rfq.status === "Awarded" ? (
      <button type="button" className="btn btn-out" onClick={() => onNavigate(`awards/${rfq.id}`)}>
        View award <Icon name="chev" size={14} />
      </button>
    ) : null;

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          RFQs
        </button>{" "}
        <Icon name="chev" size={12} /> {rfq.code}
      </div>
      <div className="pagehead">
        <div>
          <h1 style={{ display: "flex", alignItems: "center", gap: 10 }}>
            {rfq.code} <EnvelopeTag envelope={rfq.envelope} /> <SourcingStatusBadge status={rfq.status} />
          </h1>
          <p>{rfq.title || <span className="hint">(untitled)</span>}</p>
        </div>
        <div className="spacer" />
        {cta}
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}
      {rfq.envelope === "Dual" && rfq.status === "Evaluation" ? (
        <Notice tone="info" icon="lock">
          Bids sealed until opened — see Bid Openings.
        </Notice>
      ) : null}

      <div className="grid g3" style={{ marginBottom: 14 }}>
        <Stat label="Bids received" value={`${submittedCount}/${rfq.invitations.length}`} />
        <Stat label="Closes" value={dateTimeMY(rfq.closesUtc)} sub={rfq.extensionCount > 0 ? `Extended ${rfq.extensionCount}×` : undefined} />
        <Stat label="Lines" value={rfq.lines.length} />
      </div>

      {canGovern ? (
        <div className="actionbar" style={{ marginBottom: 14 }}>
          <button type="button" className="btn btn-out btn-sm" onClick={() => setInviting(true)}>
            <Icon name="plus" size={14} /> Add vendor
          </button>
          {rfq.status === "Open" ? (
            <>
              <button type="button" className="btn btn-out btn-sm" onClick={() => setExtending(true)}>
                Extend deadline
              </button>
              <button type="button" className="btn btn-out btn-sm" onClick={() => close.mutate()} disabled={close.isPending}>
                Close bids
              </button>
            </>
          ) : null}
          <div className="spacer" style={{ flex: 1 }} />
          <button type="button" className="btn btn-out btn-sm" style={{ color: "var(--red)" }} onClick={() => setCancelling(true)}>
            Cancel
          </button>
        </div>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Invited vendors</h3>
        </div>
        <div className="cbody">
          <table>
            <thead>
              <tr>
                <th>Vendor</th>
                <th>Status</th>
                <th>Reason</th>
                <th>Invited</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {rfq.invitations.map((i) => (
                <tr key={i.id}>
                  <td>
                    <div style={{ fontWeight: 600 }}>{i.vendorName}</div>
                    <div className="hint">{i.vendorCode}</div>
                  </td>
                  <td>
                    <SourcingStatusBadge status={i.status} />
                  </td>
                  <td className="hint" title={i.declineNote ?? i.rescindNote ?? undefined}>
                    {i.declineReasonCode ?? i.rescindReasonCode ?? "—"}
                  </td>
                  <td>{dateTimeMY(i.invitedUtc)}</td>
                  <td className="amt">
                    {canGovern && RESCINDABLE.has(i.status) ? (
                      <button type="button" className="btn btn-ghost btn-sm" onClick={() => setRescinding(i)}>
                        Rescind
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Activity</h3>
        </div>
        <div className="cbody">
          {rfq.events.length === 0 ? (
            <p className="hint" style={{ margin: 0 }}>
              No events yet.
            </p>
          ) : (
            [...rfq.events]
              .sort((a, b) => new Date(b.occurredUtc).getTime() - new Date(a.occurredUtc).getTime())
              .map((e) => (
                <div key={e.id} style={{ marginBottom: 8 }}>
                  <div style={{ fontSize: 12.5 }}>
                    <strong>{e.eventType}</strong>
                    {e.reasonCode ? <span className="hint"> · {e.reasonCode}</span> : null}
                    {e.oldClosesUtc && e.newClosesUtc ? (
                      <span className="hint">
                        {" "}
                        · {dateTimeMY(e.oldClosesUtc)} → {dateTimeMY(e.newClosesUtc)}
                      </span>
                    ) : null}
                  </div>
                  <div className="hint">{dateTimeMY(e.occurredUtc)}</div>
                </div>
              ))
          )}
        </div>
      </div>

      {inviting ? (
        <Modal
          title="Invite vendor"
          icon="plus"
          footer={
            <button type="button" className="btn btn-out" onClick={() => setInviting(false)}>
              Close
            </button>
          }
        >
          <div className="field">
            <label>Search vendors</label>
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
        </Modal>
      ) : null}

      {rescinding ? (
        <ConfirmModal
          title={`Rescind ${rescinding.vendorName}'s invitation`}
          icon="flag"
          body={
            <>
              <div className="field">
                <label>Reason</label>
                <select value={reasonCode} onChange={(e) => setReasonCode(e.target.value)}>
                  <option value="">Select a reason…</option>
                  {rescindReasons?.map((r) => (
                    <option key={r.code} value={r.code}>
                      {r.label}
                    </option>
                  ))}
                </select>
              </div>
              <div className="field" style={{ marginBottom: 0 }}>
                <label>Note (optional)</label>
                <textarea rows={2} value={note} onChange={(e) => setNote(e.target.value)} />
              </div>
            </>
          }
          confirmLabel="Rescind"
          danger
          busy={rescind.isPending}
          onCancel={() => {
            setRescinding(null);
            setReasonCode("");
            setNote("");
          }}
          onConfirm={() => reasonCode && rescind.mutate()}
        />
      ) : null}

      {extending ? (
        <ConfirmModal
          title="Extend deadline"
          icon="clock"
          body={
            <>
              <Kv k="Current close" v={dateTimeMY(rfq.closesUtc)} />
              <div className="field">
                <label>New close date/time</label>
                <input type="datetime-local" value={newClosesUtc} onChange={(e) => setNewClosesUtc(e.target.value)} />
              </div>
              <div className="field">
                <label>Reason (optional)</label>
                <select value={reasonCode} onChange={(e) => setReasonCode(e.target.value)}>
                  <option value="">None</option>
                  {extendReasons?.map((r) => (
                    <option key={r.code} value={r.code}>
                      {r.label}
                    </option>
                  ))}
                </select>
              </div>
            </>
          }
          confirmLabel="Extend"
          busy={extend.isPending}
          onCancel={() => {
            setExtending(false);
            setReasonCode("");
          }}
          onConfirm={() => newClosesUtc && extend.mutate()}
        />
      ) : null}

      {cancelling ? (
        <ConfirmModal
          title="Cancel RFQ"
          icon="x"
          body="This returns any sourced PR lines to Open and cannot be undone."
          confirmLabel="Cancel RFQ"
          danger
          busy={cancel.isPending}
          onCancel={() => setCancelling(false)}
          onConfirm={() => cancel.mutate()}
        />
      ) : null}
    </>
  );
}
