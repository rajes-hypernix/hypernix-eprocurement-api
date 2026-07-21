import { useNavigate, useParams } from "react-router-dom";
import { OnboardingQueuePage } from "@/pages/onboarding/OnboardingQueuePage";
import { OnboardingInvitePage } from "@/pages/onboarding/OnboardingInvitePage";
import { OnboardingReviewPage } from "@/pages/onboarding/OnboardingReviewPage";

/** Route switch for /onboarding and /onboarding/* — mirrors VendorsPage splat routing. */
export function OnboardingPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/onboarding${path ? `/${path}` : ""}`);

  if (route === "invite") {
    return <OnboardingInvitePage onBack={() => go("")} />;
  }
  if (route) {
    return <OnboardingReviewPage id={route} onBack={() => go("")} />;
  }

  return (
    <OnboardingQueuePage
      onNavigate={(key) => {
        if (key.startsWith("onboarding")) {
          const sub = key.slice("onboarding".length).replace(/^\//, "");
          go(sub);
          return;
        }
        void navigate(`/${key}`);
      }}
    />
  );
}
