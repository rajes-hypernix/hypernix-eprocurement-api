import { useEffect, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { getOnboardingLookups, resolveOnboardingLink } from "@/api/onboarding";
import { Icon } from "@/components/Icon";
import { OnboardingForm } from "@/pages/onboarding/portal/OnboardingForm";
import { OnboardingLanding } from "@/pages/onboarding/portal/OnboardingLanding";
import { OnboardingResubmit } from "@/pages/onboarding/portal/OnboardingResubmit";
import { tokenFromUrl } from "@/pages/onboarding/portal/token";

/**
 * Public vendor onboarding portal — no AppShell. Token-scoped: landing → form → submitted,
 * or clarification resubmit when status is ClarificationRequested.
 */
export function OnboardingPortalPage() {
  const token = tokenFromUrl();
  const qc = useQueryClient();
  const [screen, setScreen] = useState<"landing" | "form" | "done">("landing");

  const { data: app } = useQuery({
    queryKey: ["onboarding-resolve", token],
    queryFn: () => resolveOnboardingLink(token),
    enabled: token.length > 0,
    retry: false,
  });

  const { data: lookups } = useQuery({
    queryKey: ["onboarding-lookups", token],
    queryFn: () => getOnboardingLookups(token),
    enabled: token.length > 0,
    retry: false,
    staleTime: Infinity,
  });

  useEffect(() => {
    if (!lookups?.swec) return;
    qc.setQueryData(["swec"], lookups.swec);
  }, [lookups, qc]);

  if (screen === "done") return <OnboardingSubmitted />;
  if (screen === "form") {
    return <OnboardingForm token={token} onSubmitted={() => setScreen("done")} />;
  }
  if (app?.status === "ClarificationRequested") {
    return (
      <OnboardingResubmit token={token} app={app} onDone={() => setScreen("done")} />
    );
  }
  return <OnboardingLanding onStart={() => setScreen("form")} />;
}

function OnboardingSubmitted() {
  return (
    <div className="onboard-land">
      <div className="onboard-card">
        <div className="onboard-badge" style={{ background: "var(--green)" }}>
          <Icon name="check" size={26} />
        </div>
        <h1>Application submitted</h1>
        <p className="hint">
          Your application is now with SPSB procurement for review. You&apos;ll be emailed if they
          approve, reject, or need clarification. If they request changes, this link will show
          exactly what to fix.
        </p>
      </div>
    </div>
  );
}
