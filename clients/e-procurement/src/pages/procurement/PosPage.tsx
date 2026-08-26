import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { PoListPage } from "@/pages/procurement/PoListPage";
import { PoDetailPage } from "@/pages/procurement/PoDetailPage";
import { PoRecordPage } from "@/pages/procurement/PoRecordPage";
import { PoStandaloneFormPage } from "@/pages/procurement/PoStandaloneFormPage";

/** Route switch for /pos and /pos/* — buyers land on the record form; fulfilment is /pos/detail/:id. */
export function PosPage() {
  const navigate = useNavigate();
  const { isVendor } = useAuth();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/pos${path ? `/${path}` : ""}`);

  if (route === "new") {
    return <PoStandaloneFormPage onBack={() => go("")} onCreated={(id) => go(id)} />;
  }

  if (route.startsWith("detail/")) {
    const id = route.slice("detail/".length);
    return (
      <PoDetailPage
        id={id}
        onBack={() => go("")}
        onNavigate={(key) => void navigate(`/${key}`)}
        onOpenForm={isVendor ? undefined : () => go(id)}
      />
    );
  }

  if (route) {
    return isVendor ? (
      <PoDetailPage id={route} onBack={() => go("")} onNavigate={(key) => void navigate(`/${key}`)} />
    ) : (
      <PoRecordPage id={route} onBack={() => go("")} onOpenFulfilment={() => go(`detail/${route}`)} />
    );
  }

  return <PoListPage onOpen={(id) => go(id)} onNavigate={(key) => void navigate(`/${key}`)} onNewStandalone={() => go("new")} />;
}
