import { useNavigate, useParams } from "react-router-dom";
import { InvitationsListPage } from "@/pages/vendor/InvitationsListPage";
import { BidFormPage } from "@/pages/vendor/BidFormPage";

/** Route switch for the vendor "bids" (My Bids) nav entry. */
export function MyBidsPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  if (route) {
    return <BidFormPage rfqId={route} onBack={() => void navigate("/bids")} />;
  }

  return <InvitationsListPage mode="bids" onOpen={(rfqId) => void navigate(`/bids/${rfqId}`)} />;
}
