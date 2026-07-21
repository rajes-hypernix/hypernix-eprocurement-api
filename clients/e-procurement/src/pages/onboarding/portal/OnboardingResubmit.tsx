import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import {
  resubmitOnboardingDraft,
  type OnboardingApplicationDto,
} from "@/api/onboarding";
import { Icon } from "@/components/Icon";
import { Notice } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";

export function OnboardingResubmit({
  token,
  app,
  onDone,
}: {
  token: string;
  app: OnboardingApplicationDto;
  onDone: () => void;
}) {
  const round = app.rounds.find((r) => r.status === "Open" && r.direction === "BuyerToVendor");
  const [responses, setResponses] = useState<string[]>(round ? round.items.map(() => "") : []);
  const [err, setErr] = useState<string | null>(null);

  const submit = useMutation({
    mutationFn: () => resubmitOnboardingDraft(token, responses),
    onSuccess: onDone,
    onError: (e: Error) =>
      setErr(e instanceof ApiRequestError ? e.message : e.message),
  });

  if (!round) return null;

  return (
    <div className="ob-shell">
      <div className="pagehead">
        <div>
          <h1>Clarification requested</h1>
          <p>
            SPSB procurement asked for {round.items.length} item
            {round.items.length === 1 ? "" : "s"}. Fix them all and resubmit in one go.
          </p>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}
      <Notice tone="warn" icon="flag">
        {round.message} — requested by {round.raisedByName}
      </Notice>

      <div className="card">
        <div className="cbody">
          {round.items.map((it, i) => (
            <div className="q" key={i}>
              <div className="qt">{it.topic}</div>
              <div className="hint" style={{ margin: "2px 0 8px" }}>
                {it.request}
              </div>
              <input
                value={responses[i] ?? ""}
                aria-label={`Response to ${it.topic}`}
                placeholder="Your response / note"
                onChange={(e) =>
                  setResponses((xs) => xs.map((v, x) => (x === i ? e.target.value : v)))
                }
              />
            </div>
          ))}
        </div>
      </div>

      <div className="actionbar">
        <span className="hint">All items are sent back together.</span>
        <div className="spacer" style={{ flex: 1 }} />
        <button
          type="button"
          className="btn btn-pri"
          disabled={submit.isPending}
          onClick={() => submit.mutate()}
        >
          <Icon name="check" size={15} /> Resubmit all ({round.items.length})
        </button>
      </div>
    </div>
  );
}
