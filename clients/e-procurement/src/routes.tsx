import { createBrowserRouter, Navigate } from "react-router-dom";
import { ProtectedRoute } from "@/auth/protected-route";
import { AppShell } from "@/components/AppShell";
import { LoginPage } from "@/pages/LoginPage";
import { PlaceholderPage } from "@/pages/PlaceholderPage";
import { VendorsPage } from "@/pages/vendors/VendorsPage";
import { OnboardingPage } from "@/pages/onboarding/OnboardingPage";
import { OnboardingPortalPage } from "@/pages/onboarding/portal/OnboardingPortalPage";

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
          { path: "vendors/*", element: <VendorsPage /> },
          { path: "onboarding/*", element: <OnboardingPage /> },
          { path: ":pageKey", element: <PlaceholderPage /> },
          { path: ":pageKey/*", element: <PlaceholderPage /> },
        ],
      },
    ],
  },
  // Catch-all for unknown paths inside the SPA — don't steal /onboard (declared above).
  { path: "*", element: <Navigate to="/login" replace /> },
]);
