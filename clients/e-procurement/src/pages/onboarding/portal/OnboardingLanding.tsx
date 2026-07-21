import { useQuery } from "@tanstack/react-query";
import { resolveOnboardingLink } from "@/api/onboarding";
import { Icon } from "@/components/Icon";
import { Spinner } from "@/components/ui";
import { formatVendorType, isSwecType } from "@/lib/format";
import { tokenFromUrl } from "@/pages/onboarding/portal/token";

export function OnboardingLanding({ onStart }: { onStart: () => void }) {
  const token = tokenFromUrl();
  const { data: app, isPending, error } = useQuery({
    queryKey: ["onboarding-resolve", token],
    queryFn: () => resolveOnboardingLink(token),
    enabled: token.length > 0,
    retry: false,
  });

  const templateCount = app?.selectedTemplateIds.length ?? 0;

  return (
    <div className="onboard-land">
      <div className="onboard-card">
        <div className="onboard-badge">
          <Icon name="vendor" size={26} />
        </div>
        <h1>Welcome to SPSB supplier onboarding</h1>

        {!token ? (
          <p className="hint">
            This onboarding link is missing its access token. Please use the link from your invitation
            email.
          </p>
        ) : isPending ? (
          <Spinner label="Opening your application…" />
        ) : error ? (
          <div className="ribbon ribbon-error" style={{ justifyContent: "center" }}>
            <Icon name="x" size={14} /> {(error as Error).message || "This onboarding link is not valid or has expired."}
          </div>
        ) : app ? (
          <>
            <p className="hint">
              You&apos;ve been invited to register as a supplier. This link is secure and expires in 14
              days — no password needed. You can save and return any time via the same link.
            </p>
            <div className="card" style={{ textAlign: "left", marginTop: 20 }}>
              <div className="cbody">
                <div className="kv">
                  <span className="k">Application</span>
                  <span className="v mono">{app.code}</span>
                </div>
                <div className="kv">
                  <span className="k">Registration type</span>
                  <span className="v">{formatVendorType(app.type)}</span>
                </div>
                <div className="kv">
                  <span className="k">You&apos;ll provide</span>
                  <span className="v" style={{ textAlign: "right" }}>
                    Company &amp; banking, documents
                    {!isSwecType(app.type) ? ", 3-yr financials" : ""}
                    {templateCount > 0
                      ? `, ${templateCount} question set${templateCount === 1 ? "" : "s"}`
                      : ""}
                  </span>
                </div>
              </div>
            </div>
            <button type="button" className="btn btn-pri" style={{ marginTop: 18 }} onClick={onStart}>
              Start onboarding <Icon name="chev" size={15} />
            </button>
          </>
        ) : null}
      </div>
    </div>
  );
}
