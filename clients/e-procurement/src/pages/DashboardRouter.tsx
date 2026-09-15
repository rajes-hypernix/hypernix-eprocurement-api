import { useAuth } from "@/auth/use-auth";
import { BuyerDashboardPage } from "@/pages/BuyerDashboardPage";
import { EvaluatorDashboardPage } from "@/pages/EvaluatorDashboardPage";
import { MyRfqsPage } from "@/pages/vendor/MyRfqsPage";
import { VendorDashboardPage } from "@/pages/vendor/VendorDashboardPage";
import { useParams } from "react-router-dom";

/** Shared "dashboard" nav key — vendors get a work-queue home; evaluators get openings; buyers get KPI home. */
export function DashboardRouter() {
  const { isVendor, isEvaluatorWorkspace } = useAuth();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  if (isVendor) {
    if (route) return <MyRfqsPage />;
    return <VendorDashboardPage />;
  }
  if (isEvaluatorWorkspace) return <EvaluatorDashboardPage />;
  return <BuyerDashboardPage />;
}
