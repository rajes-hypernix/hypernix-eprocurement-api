import { useNavigate, useParams } from "react-router-dom";
import { PoListPage } from "@/pages/procurement/PoListPage";
import { PoDetailPage } from "@/pages/procurement/PoDetailPage";

/** Route switch for /pos and /pos/* */
export function PosPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/pos${path ? `/${path}` : ""}`);

  if (route) {
    return (
      <PoDetailPage
        id={route}
        onBack={() => go("")}
        onNavigate={(key) => void navigate(`/${key}`)}
      />
    );
  }

  return <PoListPage onOpen={(id) => go(id)} />;
}
