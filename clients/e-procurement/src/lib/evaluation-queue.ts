import type { RfqListItemDto } from "@/api/sourcing";

const RELEVANT = new Set(["Closed", "Evaluation", "Awarded"]);

export function rfqIsReadyToOpen(r: RfqListItemDto): boolean {
  if (RELEVANT.has(r.status)) return true;
  if (r.status !== "Open" || !r.closesUtc) return false;
  return Date.parse(r.closesUtc) <= Date.now();
}

export function rfqAssignedToUser(r: RfqListItemDto, userId: string | undefined): boolean {
  if (!userId) return false;
  const tech = r.technicalEvaluatorIds;
  const comm = r.commercialEvaluatorIds;
  if (tech == null && comm == null) return true;
  const me = userId.toLowerCase();
  return (
    (tech ?? []).some((id) => id.toLowerCase() === me) ||
    (comm ?? []).some((id) => id.toLowerCase() === me)
  );
}
