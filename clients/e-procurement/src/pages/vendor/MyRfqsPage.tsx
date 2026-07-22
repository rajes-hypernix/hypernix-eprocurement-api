import { useNavigate, useParams } from "react-router-dom";
import { InvitationsListPage } from "@/pages/vendor/InvitationsListPage";
import { BidFormPage } from "@/pages/vendor/BidFormPage";

/** Route switch for the vendor "dashboard" (My RFQs) nav entry. */
export function MyRfqsPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  if (route) {
    return <BidFormPage rfqId={route} onBack={() => void navigate("/dashboard")} />;
  }

  return <InvitationsListPage mode="invitations" onOpen={(rfqId) => void navigate(`/dashboard/${rfqId}`)} />;
}
