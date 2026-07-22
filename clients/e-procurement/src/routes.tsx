import { createBrowserRouter, Navigate } from "react-router-dom";
import { ProtectedRoute } from "@/auth/protected-route";
import { AppShell } from "@/components/AppShell";
import { LoginPage } from "@/pages/LoginPage";
import { PlaceholderPage } from "@/pages/PlaceholderPage";
import { DashboardRouter } from "@/pages/DashboardRouter";
import { VendorsPage } from "@/pages/vendors/VendorsPage";
import { OnboardingPage } from "@/pages/onboarding/OnboardingPage";
import { OnboardingPortalPage } from "@/pages/onboarding/portal/OnboardingPortalPage";
import { RequisitionsPage } from "@/pages/sourcing/RequisitionsPage";
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
          { path: "views", element: <ComingSoonRoute pageKey="views" /> },
          { path: "forms", element: <ComingSoonRoute pageKey="forms" /> },
          { path: "admin", element: <ComingSoonRoute pageKey="admin" /> },
          { path: "lists", element: <ComingSoonRoute pageKey="lists" /> },
          { path: "customfields", element: <ComingSoonRoute pageKey="customfields" /> },
          { path: "segments", element: <ComingSoonRoute pageKey="segments" /> },
          { path: "items", element: <ComingSoonRoute pageKey="items" /> },
          { path: "entryforms", element: <ComingSoonRoute pageKey="entryforms" /> },
          { path: "numbering", element: <ComingSoonRoute pageKey="numbering" /> },
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
