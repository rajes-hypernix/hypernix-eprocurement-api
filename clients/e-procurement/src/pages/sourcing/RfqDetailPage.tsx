import { useQuery } from "@tanstack/react-query";
import { getRfq } from "@/api/sourcing";
import { Spinner } from "@/components/ui";
import { RfqDraftEditPage } from "@/pages/sourcing/RfqDraftEditPage";
import { RfqDetailHubPage } from "@/pages/sourcing/RfqDetailHubPage";

/** Dispatches to the draft editor while Draft, else the post-release detail hub. */
export function RfqDetailPage({
  id,
  onBack,
  onNavigate,
}: {
  id: string;
  onBack: () => void;
  onNavigate: (key: string) => void;
}) {
  const { data: rfq, isPending } = useQuery({ queryKey: ["rfq", id], queryFn: () => getRfq(id) });

  if (isPending || !rfq) return <Spinner label="Loading RFQ…" />;

  return rfq.status === "Draft" ? (
    <RfqDraftEditPage rfq={rfq} onBack={onBack} onReleased={() => onNavigate(`rfqs/${id}`)} />
  ) : (
    <RfqDetailHubPage rfq={rfq} onBack={onBack} onNavigate={onNavigate} />
  );
}
