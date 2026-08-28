import { Navigate, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { BidOpeningsListPage } from "@/pages/sourcing/evaluation/BidOpeningsListPage";
import { BidOpeningDetailPage } from "@/pages/sourcing/evaluation/BidOpeningDetailPage";
import { TechnicalScoringPage } from "@/pages/sourcing/evaluation/TechnicalScoringPage";

/** Route switch for /openings and /openings/* */
export function EvaluationPage() {
  const { isVendor } = useAuth();
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");
  const segments = route.split("/").filter(Boolean);

  if (isVendor) return <Navigate to="/dashboard" replace />;

  const go = (path: string) => void navigate(`/openings${path ? `/${path}` : ""}`);

  if (segments.length === 2 && segments[1] === "score") {
    return (
      <TechnicalScoringPage
        rfqId={segments[0]!}
        onBack={() => go(segments[0]!)}
        onOpenCommercial={() => go(segments[0]!)}
      />
    );
  }
  if (segments.length === 1) {
    return <BidOpeningDetailPage rfqId={segments[0]!} onBack={() => go("")} onScore={() => go(`${segments[0]}/score`)} onAward={() => void navigate(`/awards/${segments[0]}`)} />;
  }

  return <BidOpeningsListPage onOpen={(id) => go(id)} />;
}
