import { useMemo } from "react";
import { RouterProvider } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { queryClient } from "@/lib/query-client";
import { AuthProvider } from "@/auth/auth-context";
import { GlobalLoadingBar } from "@/components/GlobalLoadingBar";
import { VersionChecker } from "@/components/VersionChecker";
import { ErrorDialogProvider } from "@/feedback/ErrorDialogContext";
import { env } from "@/env";
import { createAppRouter } from "@/routes";

export function App() {
  const router = useMemo(() => createAppRouter(env.basePath), []);

  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ErrorDialogProvider>
          <GlobalLoadingBar />
          <VersionChecker />
          <RouterProvider router={router} />
        </ErrorDialogProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
