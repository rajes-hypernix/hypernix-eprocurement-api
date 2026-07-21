import { useNavigate, useParams } from "react-router-dom";
import { VendorMasterPage } from "@/pages/vendors/VendorMasterPage";
import { VendorDetailPage } from "@/pages/vendors/VendorDetailPage";
import { NewVendorChooserPage } from "@/pages/vendors/NewVendorChooserPage";
import { ManualVendorFormPage } from "@/pages/vendors/ManualVendorFormPage";

/** Route switch for /vendors and /vendors/* — mirrors original VendorsPage. */
export function VendorsPage() {
  const navigate = useNavigate();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  const go = (key: string) => void navigate(`/${key}`);

  if (route === "new") {
    return (
      <NewVendorChooserPage
        onManual={() => go("vendors/manual")}
        onInvite={() => go("onboarding/invite")}
        onBack={() => go("vendors")}
      />
    );
  }
  if (route === "manual") {
    return (
      <ManualVendorFormPage
        onSaved={(id) => go(`vendors/${id}`)}
        onBack={() => go("vendors/new")}
      />
    );
  }
  if (route && route !== "new" && route !== "manual") {
    return <VendorDetailPage id={route} onBack={() => go("vendors")} />;
  }

  return (
    <VendorMasterPage onOpen={(id) => go(`vendors/${id}`)} onNavigate={go} />
  );
}
