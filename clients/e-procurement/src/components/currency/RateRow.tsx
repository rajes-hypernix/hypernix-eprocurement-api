import { fmt } from "@/lib/format";

/**
 * Currency-aware rate row (POC R3-S1T4b). Hidden for base currency.
 * Rate is the RFQ header snapshot; Update re-snapshots from Platform current FX.
 */
export function RateRow({
  currency,
  baseCurrency,
  exchangeRateToBase,
  total,
  editable,
  busy,
  onUpdate,
}: {
  currency?: string;
  baseCurrency?: string;
  exchangeRateToBase?: number | null;
  total: number;
  editable: boolean;
  busy: boolean;
  onUpdate: () => void;
}) {
  const base = baseCurrency ?? "MYR";
  const code = currency ?? base;
  if (!code || code === base) return null;

  const hasRate = exchangeRateToBase != null;

  return (
    <div className="rate-row" aria-label="Exchange rate">
      <div className="rate-line">
        {hasRate ? (
          <span className="rate-fact">
            Exchange rate:{" "}
            <b>
              1 {code} = {fmt(exchangeRateToBase!)} {base}
            </b>
          </span>
        ) : (
          <span className="rate-fact rate-missing">no exchange rate on file</span>
        )}
        {editable ? (
          <>
            {" "}
            ·{" "}
            <button
              type="button"
              className="btn btn-out btn-sm"
              disabled={busy}
              onClick={onUpdate}
              aria-label="Update exchange rate"
            >
              Update rate
            </button>
          </>
        ) : null}
      </div>
      {hasRate ? (
        <div className="rate-equiv hint" aria-label="Base-currency equivalent">
          ≈ {base} {fmt(total * exchangeRateToBase!)}
        </div>
      ) : null}
    </div>
  );
}
