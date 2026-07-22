import { useNavigate, useParams } from "react-router-dom";
import { InvoiceListPage } from "@/pages/procurement/InvoiceListPage";
import { InvoiceDetailPage } from "@/pages/procurement/InvoiceDetailPage";
import { InvoiceFormPage } from "@/pages/procurement/InvoiceFormPage";

/** Route switch for /invoices and /invoices/* */
export function InvoicesPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (path: string) => void navigate(`/invoices${path ? `/${path}` : ""}`);

  if (route.startsWith("new/")) {
    const poId = route.slice("new/".length);
    return <InvoiceFormPage poId={poId} onBack={() => go("")} onSaved={(id) => go(id)} />;
  }
  if (route) {
    return <InvoiceDetailPage id={route} onBack={() => go("")} />;
  }

  return <InvoiceListPage onOpen={(id) => go(id)} />;
}
