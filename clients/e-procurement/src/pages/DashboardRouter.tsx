import { useAuth } from "@/auth/use-auth";
import { BuyerDashboardPage } from "@/pages/BuyerDashboardPage";
import { MyRfqsPage } from "@/pages/vendor/MyRfqsPage";
import { VendorDashboardPage } from "@/pages/vendor/VendorDashboardPage";
import { useParams } from "react-router-dom";

/** Shared "dashboard" nav key — vendors get a work-queue home; buyers get KPI home. */
export function DashboardRouter() {
  const { isVendor } = useAuth();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  if (!isVendor) return <BuyerDashboardPage />;
  if (route) return <MyRfqsPage />;
  return <VendorDashboardPage />;
}
