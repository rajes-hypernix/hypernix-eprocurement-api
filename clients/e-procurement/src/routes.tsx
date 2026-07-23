import { createBrowserRouter, Navigate, useNavigate } from "react-router-dom";
import { ProtectedRoute } from "@/auth/protected-route";
import { AppShell } from "@/components/AppShell";
import { LoginPage } from "@/pages/LoginPage";
import { PlaceholderPage } from "@/pages/PlaceholderPage";
import { DashboardRouter } from "@/pages/DashboardRouter";
import { VendorsPage } from "@/pages/vendors/VendorsPage";
import { OnboardingPage } from "@/pages/onboarding/OnboardingPage";
import { OnboardingPortalPage } from "@/pages/onboarding/portal/OnboardingPortalPage";
import { RequisitionsPage } from "@/pages/sourcing/RequisitionsPage";
import { ConsolidatePage } from "@/pages/sourcing/ConsolidatePage";
import { RfqsPage } from "@/pages/sourcing/RfqsPage";
import { EvaluationPage } from "@/pages/sourcing/EvaluationPage";
import { AwardsPage } from "@/pages/sourcing/AwardsPage";
import { ClarificationsPage } from "@/pages/sourcing/ClarificationsPage";
import { MyBidsPage } from "@/pages/vendor/MyBidsPage";
import { PosPage } from "@/pages/procurement/PosPage";
import { DeliveriesPage } from "@/pages/procurement/DeliveriesPage";
import { InvoicesPage } from "@/pages/procurement/InvoicesPage";
import { NotificationsInboxPage } from "@/pages/notifications/NotificationsInboxPage";
import { ComingSoonRoute } from "@/pages/ComingSoonPage";
import { StatementsPage } from "@/pages/statements/StatementsPage";
import { AdminPage } from "@/pages/admin/AdminPage";
import { FormsPage } from "@/pages/setup/FormsPage";
import { AuditsPage } from "@/pages/setup/AuditsPage";
import { PlatformMastersPage } from "@/pages/setup/PlatformMastersPage";
import { SavedViewsHome } from "@/pages/views/SavedViewsHome";

/** Multi-PR RFQ consolidation workspace — reachable via the "Build RFQ" button, not the nav rail. */
function ConsolidateRoute() {
  const navigate = useNavigate();
  return (
    <ConsolidatePage onBack={() => void navigate("/reqs")} onOpenRfq={(id) => void navigate(`/rfqs/${id}`)} />
  );
}

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  { path: "/onboard", element: <OnboardingPortalPage /> },
  {
    element: <ProtectedRoute />,
    children: [
      {
        element: <AppShell />,
        children: [
          { index: true, element: <Navigate to="/dashboard" replace /> },
          { path: "dashboard/*", element: <DashboardRouter /> },
          { path: "vendors/*", element: <VendorsPage /> },
          { path: "onboarding/*", element: <OnboardingPage /> },
          { path: "reqs/*", element: <RequisitionsPage /> },
          { path: "consolidate", element: <ConsolidateRoute /> },
          { path: "rfqs/*", element: <RfqsPage /> },
          { path: "openings/*", element: <EvaluationPage /> },
          { path: "awards/*", element: <AwardsPage /> },
          { path: "chats", element: <ClarificationsPage /> },
          { path: "bids/*", element: <MyBidsPage /> },
          { path: "pos/*", element: <PosPage /> },
          { path: "deliveries/*", element: <DeliveriesPage /> },
          { path: "invoices/*", element: <InvoicesPage /> },
          { path: "notifications", element: <NotificationsInboxPage /> },
          { path: "payments", element: <ComingSoonRoute pageKey="payments" /> },
          { path: "views", element: <SavedViewsHome /> },
          { path: "forms", element: <FormsPage /> },
          { path: "admin/*", element: <AdminPage /> },
          { path: "lists", element: <Navigate to="/masters?tab=banks" replace /> },
          { path: "audits", element: <AuditsPage /> },
          { path: "masters", element: <PlatformMastersPage /> },
          { path: "configuration", element: <Navigate to="/masters?tab=settings" replace /> },
          { path: "customfields", element: <ComingSoonRoute pageKey="customfields" /> },
          { path: "segments", element: <ComingSoonRoute pageKey="segments" /> },
          { path: "items", element: <Navigate to="/masters?tab=items" replace /> },
          { path: "entryforms", element: <ComingSoonRoute pageKey="entryforms" /> },
          { path: "numbering", element: <Navigate to="/masters?tab=numbering" replace /> },
          { path: "statements/*", element: <StatementsPage /> },
          { path: "statement/*", element: <StatementsPage /> },
          { path: ":pageKey", element: <PlaceholderPage /> },
          { path: ":pageKey/*", element: <PlaceholderPage /> },
        ],
      },
    ],
  },
  // Catch-all for unknown paths inside the SPA — don't steal /onboard (declared above).
  { path: "*", element: <Navigate to="/login" replace /> },
]);
