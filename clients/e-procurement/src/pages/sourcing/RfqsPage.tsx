import { useNavigate, useParams } from "react-router-dom";
import { RfqListPage } from "@/pages/sourcing/RfqListPage";
import { RfqBuilderPage } from "@/pages/sourcing/RfqBuilderPage";
import { RfqDetailPage } from "@/pages/sourcing/RfqDetailPage";

/** Route switch for /rfqs and /rfqs/* — mirrors VendorsPage splat routing. */
export function RfqsPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/rfqs${path ? `/${path}` : ""}`);

  if (route === "new") {
    return <RfqBuilderPage onSaved={(id) => go(id)} onBack={() => go("")} />;
  }
  if (route) {
    return <RfqDetailPage id={route} onBack={() => go("")} onNavigate={(key) => void navigate(`/${key}`)} />;
  }

  return <RfqListPage onOpen={(id) => go(id)} onNew={() => go("new")} />;
}
