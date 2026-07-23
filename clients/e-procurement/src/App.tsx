import { RouterProvider } from "react-router-dom";
import { QueryClientProvider } from "@tanstack/react-query";
import { queryClient } from "@/lib/query-client";
import { AuthProvider } from "@/auth/auth-context";
import { GlobalLoadingBar } from "@/components/GlobalLoadingBar";
import { ErrorDialogProvider } from "@/feedback/ErrorDialogContext";
import { router } from "@/routes";

export function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <ErrorDialogProvider>
          <GlobalLoadingBar />
          <RouterProvider router={router} />
        </ErrorDialogProvider>
      </AuthProvider>
    </QueryClientProvider>
  );
}
