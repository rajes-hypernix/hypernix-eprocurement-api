import { useNavigate, useParams } from "react-router-dom";
import { RequisitionListPage } from "@/pages/sourcing/RequisitionListPage";
import { RequisitionFormPage } from "@/pages/sourcing/RequisitionFormPage";

/** Route switch for /reqs and /reqs/* — mirrors VendorsPage splat routing. */
export function RequisitionsPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/reqs${path ? `/${path}` : ""}`);

  if (route === "new") {
    return <RequisitionFormPage onSaved={(id) => go(id)} onBack={() => go("")} />;
  }
  if (route) {
    return <RequisitionFormPage id={route} onSaved={() => go(route)} onBack={() => go("")} />;
  }

  return (
    <RequisitionListPage
      onOpen={(id) => go(id)}
      onNew={() => go("new")}
      onNavigate={(key) => void navigate(`/${key}`)}
      onOpenRfq={(id) => void navigate(`/rfqs/${id}`)}
    />
  );
}
