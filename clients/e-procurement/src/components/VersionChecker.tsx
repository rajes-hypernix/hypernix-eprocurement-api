import { useEffect, useState } from "react";
import { ConfirmModal } from "@/components/ui";
import {
  dismissCurrentVersionPrompt,
  refreshToNewVersion,
  startVersionChecker,
} from "@/lib/version-checker";

/**
 * Surfactor-style deploy detector: polls for a new build and prompts the user to refresh.
 */
export function VersionChecker() {
  const [open, setOpen] = useState(false);

  useEffect(() => {
    return startVersionChecker(() => setOpen(true));
  }, []);

  if (!open) return null;

  return (
    <ConfirmModal
      title="Update available"
      icon="alert"
      body="A new version of eProcure is available. Refresh to load the latest screens and avoid running on outdated UI."
      confirmLabel="Refresh now"
      cancelLabel="Later"
      onConfirm={() => refreshToNewVersion()}
      onCancel={() => {
        dismissCurrentVersionPrompt();
        setOpen(false);
      }}
    />
  );
}
