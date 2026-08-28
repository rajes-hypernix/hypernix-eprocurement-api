import { Navigate, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { RfqListPage } from "@/pages/sourcing/RfqListPage";
import { RfqDetailPage } from "@/pages/sourcing/RfqDetailPage";

/** Route switch for /rfqs and /rfqs/* — mirrors VendorsPage splat routing. */
export function RfqsPage() {
  const { isVendor } = useAuth();
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  if (isVendor) {
    return <Navigate to={route ? `/dashboard/${route}` : "/dashboard"} replace />;
  }

  const go = (path: string) => void navigate(`/rfqs${path ? `/${path}` : ""}`);

  // RFQ drafts are now built from the Requisitions/Consolidate flow — old deep links to
  // the single-page builder redirect to the multi-PR consolidation workspace.
  if (route === "new") {
    return <Navigate to="/consolidate" replace />;
  }
  if (route) {
    return <RfqDetailPage id={route} onBack={() => go("")} onNavigate={(key) => void navigate(`/${key}`)} />;
  }

  return <RfqListPage onOpen={(id) => go(id)} onNew={() => void navigate("/consolidate")} />;
}
