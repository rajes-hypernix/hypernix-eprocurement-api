import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  listOnboardingApplications,
  resendOnboardingInvitation,
  revokeOnboardingInvitation,
  type OnboardingInvitationDto,
} from "@/api/onboarding";
import { Icon } from "@/components/Icon";
import { ConfirmModal, EmptyState, Modal, Spinner } from "@/components/ui";
import { TypeBadge } from "@/components/vendors/badges";
import { dateMY } from "@/lib/format";
import { portalMagicLink } from "@/lib/magic-link";

const RESENDABLE = new Set(["Invited", "InProgress", "Expired"]);

const STATUS_CLS: Record<string, string> = {
  Invited: "b-grey",
  InProgress: "b-blue",
  Submitted: "b-amber",
  UnderReview: "b-teal",
  ClarificationRequested: "b-amber",
  Resubmitted: "b-blue",
  Approved: "b-green",
  Rejected: "b-red",
  Expired: "b-grey",
  Revoked: "b-grey",
  Withdrawn: "b-grey",
};

function statusLabel(s: string): string {
  return s.replace(/([a-z])([A-Z])/g, "$1 $2");
}

export function OnboardingQueuePage({ onNavigate }: { onNavigate: (key: string) => void }) {
  const qc = useQueryClient();
  const { data: apps = [], isPending } = useQuery({
    queryKey: ["onboarding-apps"],
    queryFn: listOnboardingApplications,
  });
  const [resent, setResent] = useState<OnboardingInvitationDto | null>(null);
  const [confirmRevoke, setConfirmRevoke] = useState<{ invitationId: string; code: string } | null>(
    null,
  );

  const resend = useMutation({
    mutationFn: (invitationId: string) => resendOnboardingInvitation(invitationId),
    onSuccess: setResent,
  });

  const revoke = useMutation({
    mutationFn: (invitationId: string) => revokeOnboardingInvitation(invitationId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: ["onboarding-apps"] });
      setConfirmRevoke(null);
    },
  });

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Onboarding</h1>
          <p>Applications in progress. Approve to promote into the Vendor Master.</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-pri btn-sm" onClick={() => onNavigate("vendors/new")}>
          <Icon name="plus" size={15} /> New vendor
        </button>
      </div>

      {isPending ? (
        <Spinner />
      ) : apps.length === 0 ? (
        <EmptyState icon="vendor">No onboarding applications yet. Invite a vendor to get started.</EmptyState>
      ) : (
        <div className="card">
          <table>
            <thead>
              <tr>
                <th>Application</th>
                <th>Vendor</th>
                <th>Type</th>
                <th>Received</th>
                <th>Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {apps.map((a) => (
                <tr
                  key={a.id}
                  className="drillrow"
                  onClick={() => onNavigate(`onboarding/${a.id}`)}
                >
                  <td className="mono" style={{ fontWeight: 600 }}>
                    {a.code}
                  </td>
                  <td style={{ fontWeight: 600 }}>{a.name || "—"}</td>
                  <td>
                    <TypeBadge type={a.type} />
                  </td>
                  <td className="hint">{dateMY(a.submittedUtc ?? a.createdOnUtc)}</td>
                  <td>
                    <span className={`badge ${STATUS_CLS[a.status] ?? "b-grey"}`}>
                      {statusLabel(a.status)}
                    </span>
                    {a.openRoundNo != null ? <span className="hint"> · rd {a.openRoundNo}</span> : null}
                  </td>
                  <td style={{ textAlign: "right" }}>
                    <div className="rowactions">
                      {RESENDABLE.has(a.status) && a.invitationId ? (
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm"
                          disabled={resend.isPending}
                          onClick={(e) => {
                            e.stopPropagation();
                            resend.mutate(a.invitationId!);
                          }}
                        >
                          <Icon name="send" size={13} /> Resend link
                        </button>
                      ) : null}
                      {RESENDABLE.has(a.status) && a.invitationId ? (
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm"
                          style={{ color: "var(--red)" }}
                          onClick={(e) => {
                            e.stopPropagation();
                            setConfirmRevoke({ invitationId: a.invitationId!, code: a.code });
                          }}
                        >
                          <Icon name="x" size={13} /> Revoke
                        </button>
                      ) : null}
                      <button
                        type="button"
                        className="btn btn-out btn-sm"
                        onClick={(e) => {
                          e.stopPropagation();
                          onNavigate(`onboarding/${a.id}`);
                        }}
                      >
                        Review <Icon name="chev" size={13} />
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      {confirmRevoke ? (
        <ConfirmModal
          icon="x"
          title={`Revoke the invitation for ${confirmRevoke.code}?`}
          body="This permanently disables the vendor's magic link — it will stop resolving and they can no longer open or submit their application. The row stays visible as Revoked. You can re-invite later if needed."
          cancelLabel="Keep active"
          confirmLabel="Revoke link"
          danger
          busy={revoke.isPending}
          onCancel={() => setConfirmRevoke(null)}
          onConfirm={() => revoke.mutate(confirmRevoke.invitationId)}
        />
      ) : null}

      {resent ? (
        <Modal
          title="Link resent"
          icon="send"
          footer={
            <button type="button" className="btn btn-pri" onClick={() => setResent(null)}>
              Done
            </button>
          }
        >
          <p style={{ marginTop: 0 }}>
            A fresh secure link for <b>{resent.applicationCode}</b> has been emailed to{" "}
            <b>{resent.email}</b> (expires in 14 days).
          </p>
          <div className="field" style={{ marginBottom: 0 }}>
            <label>Magic link (demo)</label>
            <input
              readOnly
              value={portalMagicLink(resent.magicLink)}
              aria-label="Magic link"
              onFocus={(e) => e.target.select()}
            />
            <p className="hint" style={{ marginTop: 8 }}>
              Open this URL in a new tab (vendor portal — no login).
            </p>
          </div>
        </Modal>
      ) : null}
    </>
  );
}
