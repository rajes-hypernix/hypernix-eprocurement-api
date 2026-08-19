import { useCallback, useEffect, useRef, type ReactNode } from "react";
import { useBlocker } from "react-router-dom";
import { ConfirmModal } from "@/components/ui";

/**
 * Snapshot dirty signal — baseline captured once when ready, then compared by JSON.
 */
export function useDirtyState<T>(current: T, ready = true): {
  isDirty: boolean;
  markClean: () => void;
} {
  const baseline = useRef<string | null>(null);
  const json = JSON.stringify(current);
  if (ready && baseline.current === null) baseline.current = json;
  const isDirty = baseline.current !== null && json !== baseline.current;
  const markClean = useCallback(() => {
    baseline.current = json;
  }, [json]);
  return { isDirty, markClean };
}

/**
 * Shared unsaved-changes leave guard (POC R4-S0): in-app via useBlocker + ConfirmModal;
 * browser unload via beforeunload.
 */
export function useUnsavedChangesGuard({
  isDirty,
  when = true,
}: {
  isDirty: boolean;
  when?: boolean;
}): { guardDialog: ReactNode; markClean: () => void } {
  const dismissed = useRef(false);
  if (!(when && isDirty)) dismissed.current = false;
  const active = when && isDirty && !dismissed.current;

  const blocker = useBlocker(active);

  useEffect(() => {
    const onUnload = (e: BeforeUnloadEvent) => {
      if (!active) return;
      e.preventDefault();
    };
    window.addEventListener("beforeunload", onUnload);
    return () => window.removeEventListener("beforeunload", onUnload);
  }, [active]);

  const markClean = useCallback(() => {
    dismissed.current = true;
  }, []);

  const guardDialog =
    blocker.state === "blocked" ? (
      <ConfirmModal
        title="Unsaved changes"
        icon="edit"
        body="You have unsaved changes that will be lost if you leave. Save your changes first, or continue without saving?"
        cancelLabel="Stay on page"
        confirmLabel="Continue without saving"
        onCancel={() => blocker.reset()}
        onConfirm={() => {
          dismissed.current = true;
          blocker.proceed();
        }}
      />
    ) : null;

  return { guardDialog, markClean };
}
