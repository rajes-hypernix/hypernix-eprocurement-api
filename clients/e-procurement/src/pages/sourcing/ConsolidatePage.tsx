import { createRfqDraft, type RfqLineInput } from "@/api/sourcing";
import { SourcingLinePicker } from "./SourcingLinePicker";

/**
 * RFQ Workspace page — thin wrapper that delegates all picker logic to
 * SourcingLinePicker (mode="workspace"). This component owns the Build RFQ
 * / Build-all mutations and the navigation callbacks.
 */
export function ConsolidatePage({
  onOpenRfq,
  onBack,
  onOpenRfqList,
}: {
  onOpenRfq: (id: string) => void;
  onBack: () => void;
  onOpenRfqList?: () => void;
}) {
  const buildOne = async (title: string, lines: RfqLineInput[], prRefs: string[]) => {
    const id = await createRfqDraft({ title, envelope: "Dual", currency: "MYR", prRefs, lines });
    return id;
  };

  const buildAllFn = async (batches: { title: string; lines: RfqLineInput[]; prRefs: string[] }[]) => {
    for (const batch of batches) {
      await createRfqDraft({ title: batch.title, envelope: "Dual", currency: "MYR", prRefs: batch.prRefs, lines: batch.lines });
    }
  };

  return (
    <>
      <SourcingLinePicker
        mode="workspace"
        onBuild={buildOne}
        onBuildAll={buildAllFn}
        onBack={onBack}
        onOpenRfq={onOpenRfq}
        onOpenRfqList={onOpenRfqList}
      />
    </>
  );
}
