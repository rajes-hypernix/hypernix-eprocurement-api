import { useNavigate, useParams } from "react-router-dom";
import { DeliveryListPage } from "@/pages/procurement/DeliveryListPage";
import { AsnDetailPage } from "@/pages/procurement/AsnDetailPage";
import { AsnFormPage } from "@/pages/procurement/AsnFormPage";
import { ReceiveAsnPage } from "@/pages/procurement/ReceiveAsnPage";

/** Route switch for /deliveries and /deliveries/* */
export function DeliveriesPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/deliveries${path ? `/${path}` : ""}`);

  if (route.startsWith("asn/")) {
    const id = route.slice("asn/".length);
    return <AsnDetailPage id={id} onBack={() => go("")} onReceive={() => go(`receive/${id}`)} />;
  }
  if (route.startsWith("new/")) {
    const poId = route.slice("new/".length);
    return <AsnFormPage poId={poId} onBack={() => go("")} />;
  }
  if (route.startsWith("receive/")) {
    const asnId = route.slice("receive/".length);
    return <ReceiveAsnPage asnId={asnId} onBack={() => go(`asn/${asnId}`)} onDone={() => go(`asn/${asnId}`)} />;
  }

  return (
    <DeliveryListPage
      onOpen={(id) => go(`asn/${id}`)}
      onNewAsn={(poId) => go(`new/${poId}`)}
      onReceive={(asnId) => go(`receive/${asnId}`)}
    />
  );
}
