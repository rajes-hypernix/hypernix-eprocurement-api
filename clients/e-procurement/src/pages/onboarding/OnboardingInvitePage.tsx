import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { createOnboardingInvitation, type OnboardingInvitationDto } from "@/api/onboarding";
import { listFormTemplates } from "@/api/platform";
import { Icon } from "@/components/Icon";
import { Modal, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";

const DEFAULT_EMAIL = "vendor-invites@hypernix.test";

const DOCS: { name: string; req: string; tone: string }[] = [
  { name: "SSM / CCM Registration", req: "Required", tone: "b-red" },
  { name: "ISO 9001:2015 Certificate", req: "Optional", tone: "b-grey" },
  { name: "CIDB Grade Certificate", req: "Optional", tone: "b-grey" },
  { name: "Bank Confirmation Letter", req: "Required", tone: "b-red" },
  { name: "Audited Accounts (3 years)", req: "Non-SWEC", tone: "b-blue" },
  { name: "PETRONAS SWEC Certificate", req: "SWEC", tone: "b-blue" },
];

type VendorType = "Swec" | "NonSwec";

export function OnboardingInvitePage({ onBack }: { onBack: () => void }) {
  const { data: templates = [], isPending } = useQuery({
    queryKey: ["form-templates"],
    queryFn: () => listFormTemplates(),
  });

  const [type, setType] = useState<VendorType>("NonSwec");
  const [email, setEmail] = useState(DEFAULT_EMAIL);
  const [deselected, setDeselected] = useState<Set<string>>(new Set());
  const [sent, setSent] = useState<OnboardingInvitationDto | null>(null);
  const [err, setErr] = useState<string | null>(null);

  const isSelected = (id: string) => !deselected.has(id);
  const toggle = (id: string) =>
    setDeselected((prev) => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  const selectedIds = templates.filter((t) => isSelected(t.id)).map((t) => t.id);

  const send = useMutation({
    mutationFn: () =>
      createOnboardingInvitation({
        email: email.trim(),
        type,
        selectedTemplateIds: selectedIds,
      }),
    onSuccess: (inv) => setSent(inv),
    onError: (e: Error) =>
      setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  const submit = () => {
    setErr(null);
    if (!email.trim()) {
      setErr("Enter the vendor email.");
      return;
    }
    send.mutate();
  };

  if (isPending) return <Spinner />;

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Onboarding
        </button>{" "}
        <Icon name="chev" size={12} /> Invite vendor
      </div>
      <div className="pagehead">
        <div>
          <h1>Invite vendor</h1>
          <p>Compose the onboarding pack and email the supplier a secure magic link.</p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="grid g2">
            <div className="field">
              <label>
                Vendor email <span className="req">*</span>
              </label>
              <input
                type="email"
                value={email}
                aria-label="Vendor email"
                onChange={(e) => setEmail(e.target.value)}
              />
            </div>
          </div>
          <div className="field" style={{ marginBottom: 0 }}>
            <label>Registration type</label>
            <div className="segmented">
              <button
                type="button"
                className={`btn btn-sm ${type === "NonSwec" ? "btn-pri" : "btn-out"}`}
                onClick={() => setType("NonSwec")}
              >
                Non-SWEC
              </button>
              <button
                type="button"
                className={`btn btn-sm ${type === "Swec" ? "btn-pri" : "btn-out"}`}
                onClick={() => setType("Swec")}
              >
                PETRONAS SWEC
              </button>
            </div>
            <p className="hint" style={{ marginTop: 7 }}>
              {type === "Swec"
                ? "SWEC — pre-vetted on the PETRONAS LLRC; financial pre-qualification is waived."
                : "Non-SWEC — financial pre-qualification (ratios + Altman Z-score) is included automatically."}
            </p>
          </div>
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Question packs</h3>
          <span className="sub">· added on top of core details</span>
        </div>
        <div className="cbody">
          {templates.length === 0 ? (
            <p className="muted">No onboarding question packs published yet.</p>
          ) : (
            templates.map((t) => (
              <label key={t.id} className="pickrow" style={{ cursor: "pointer" }}>
                <input
                  type="checkbox"
                  checked={isSelected(t.id)}
                  aria-label={t.name}
                  onChange={() => toggle(t.id)}
                />
                <div style={{ flex: 1 }}>
                  <div style={{ fontWeight: 600, fontSize: 13 }}>{t.name}</div>
                  <div className="hint">
                    {t.questionCount} question{t.questionCount === 1 ? "" : "s"}
                  </div>
                </div>
              </label>
            ))
          )}
        </div>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Documents requested</h3>
        </div>
        <div className="cbody">
          {DOCS.map((d) => (
            <div key={d.name} className="doc">
              <div className="di">
                <Icon name="doc" size={15} />
              </div>
              <div className="dn">{d.name}</div>
              <span className={`badge ${d.tone}`}>{d.req}</span>
            </div>
          ))}
        </div>
      </div>

      <div className="actionbar">
        <span className="hint">
          <Icon name="clip" size={13} /> A secure magic link (14-day expiry) is emailed — no password
          needed until approved.
        </span>
        <div className="spacer" style={{ flex: 1 }} />
        <button type="button" className="btn btn-out" onClick={onBack}>
          Back
        </button>
        <button type="button" className="btn btn-pri" disabled={send.isPending} onClick={submit}>
          <Icon name="send" size={15} /> Generate &amp; send link
        </button>
      </div>

      {sent ? (
        <Modal
          title="Invitation sent"
          icon="send"
          footer={
            <>
              <button type="button" className="btn btn-out" onClick={() => setSent(null)}>
                Send another
              </button>
              <button type="button" className="btn btn-pri" onClick={onBack}>
                Done
              </button>
            </>
          }
        >
          <p style={{ marginTop: 0 }}>
            <b>{sent.applicationCode}</b> · a secure onboarding link has been emailed to{" "}
            <b>{sent.email}</b>. It expires in 14 days. Only that inbox can open the application.
          </p>
        </Modal>
      ) : null}
    </>
  );
}
