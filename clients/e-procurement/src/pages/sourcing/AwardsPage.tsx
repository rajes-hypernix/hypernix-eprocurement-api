import { useNavigate, useParams } from "react-router-dom";
import { AwardsListPage } from "@/pages/sourcing/award/AwardsListPage";
import { AwardWorkspacePage } from "@/pages/sourcing/award/AwardWorkspacePage";

/** Route switch for /awards and /awards/* */
export function AwardsPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/awards${path ? `/${path}` : ""}`);

  if (route) {
    return <AwardWorkspacePage rfqId={route} onBack={() => go("")} />;
  }

  return <AwardsListPage onOpen={(id) => go(id)} />;
}
