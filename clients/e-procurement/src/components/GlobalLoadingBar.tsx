import { useIsFetching, useIsMutating } from "@tanstack/react-query";

/**
 * Thin top progress bar while any React Query fetch or mutation is in flight.
 * Page-local Spinners can remain for first-load empty states; this covers the global case.
 */
export function GlobalLoadingBar() {
  const fetching = useIsFetching();
  const mutating = useIsMutating();
  const busy = fetching + mutating > 0;
  if (!busy) return null;
  return <div className="global-loading" role="progressbar" aria-label="Loading" aria-busy="true" />;
}
