import { createContext, useCallback, useContext, useMemo, useState, type ReactNode } from "react";
import { AlertModal } from "@/components/ui";
import { ApiRequestError, messageFromProblem } from "@/lib/api-client";

type ErrorDialogState = {
  title: string;
  message: string;
};

type ErrorDialogApi = {
  /** Show a blocking error dialog (standard for add/edit/delete failures). */
  showError: (message: string, title?: string) => void;
  /** Extract a user-facing message and show the dialog. */
  showErrorFrom: (error: unknown, title?: string) => void;
};

const ErrorDialogContext = createContext<ErrorDialogApi | null>(null);

/** Prefer ProblemDetails validation / Identity error lists over generic wrappers. */
export function formatApiError(e: unknown): string {
  if (e instanceof ApiRequestError) {
    return messageFromProblem(e.problem, e.message || "Something went wrong.");
  }
  if (e instanceof Error) return e.message;
  return "Something went wrong.";
}

export function ErrorDialogProvider({ children }: { children: ReactNode }) {
  const [state, setState] = useState<ErrorDialogState | null>(null);

  const showError = useCallback((message: string, title = "Error") => {
    const msg = message.trim() || "Something went wrong.";
    setState({ title, message: msg });
  }, []);

  const showErrorFrom = useCallback(
    (error: unknown, title = "Error") => {
      showError(formatApiError(error), title);
    },
    [showError],
  );

  const api = useMemo(() => ({ showError, showErrorFrom }), [showError, showErrorFrom]);

  return (
    <ErrorDialogContext.Provider value={api}>
      {children}
      {state ? (
        <AlertModal
          title={state.title}
          icon="x"
          body={state.message}
          onClose={() => setState(null)}
        />
      ) : null}
    </ErrorDialogContext.Provider>
  );
}

export function useErrorDialog(): ErrorDialogApi {
  const ctx = useContext(ErrorDialogContext);
  if (!ctx) {
    throw new Error("useErrorDialog must be used within ErrorDialogProvider");
  }
  return ctx;
}
