import { Navigate, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { AwardsListPage } from "@/pages/sourcing/award/AwardsListPage";
import { AwardWorkspacePage } from "@/pages/sourcing/award/AwardWorkspacePage";

/** Route switch for /awards and /awards/* */
export function AwardsPage() {
  const { isVendor } = useAuth();
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  if (isVendor) return <Navigate to="/dashboard" replace />;

  const go = (path: string) => void navigate(`/awards${path ? `/${path}` : ""}`);

  if (route) {
    return <AwardWorkspacePage rfqId={route} onBack={() => go("")} />;
  }

  return <AwardsListPage onOpen={(id) => go(id)} />;
}
