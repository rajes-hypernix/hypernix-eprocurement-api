import { useAuth } from "@/auth/use-auth";
import { BuyerDashboardPage } from "@/pages/BuyerDashboardPage";
import { MyRfqsPage } from "@/pages/vendor/MyRfqsPage";

/** Shared "dashboard" nav key — vendors get My RFQs; buyers get KPI home. */
export function DashboardRouter() {
  const { isVendor } = useAuth();
  return isVendor ? <MyRfqsPage /> : <BuyerDashboardPage />;
}
