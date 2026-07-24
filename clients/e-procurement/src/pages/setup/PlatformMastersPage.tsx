import { useMemo } from "react";
import { Navigate, useSearchParams } from "react-router-dom";
import { Icon } from "@/components/Icon";
import { ConfigurationPage, CONFIG_TAB_KEYS } from "@/pages/setup/ConfigurationPage";
import { LookupsPage, LOOKUP_TAB_KEYS } from "@/pages/setup/LookupsPage";

const LOOKUP_TABS = [
  { key: "lists", icon: "list", label: "Lists" },
  { key: "countries", icon: "field", label: "Countries" },
  { key: "banks", icon: "clip", label: "Banks" },
  { key: "org", icon: "users", label: "Org" },
] as const;

const CONFIG_TABS = [
  { key: "settings", icon: "menu", label: "Settings" },
  { key: "currencies", icon: "clip", label: "Currencies" },
  { key: "rates", icon: "chart", label: "Exchange rates" },
  { key: "tax", icon: "hash", label: "Tax codes" },
  { key: "payment", icon: "send", label: "Payment terms" },
  { key: "incoterms", icon: "flag", label: "Incoterms" },
  { key: "locations", icon: "field", label: "Locations" },
  { key: "items", icon: "box", label: "Items" },
  { key: "numbering", icon: "list", label: "Numbering" },
  { key: "customFields", icon: "field", label: "Custom Fields" },
] as const;

const ALL_TABS = [...LOOKUP_TABS, ...CONFIG_TABS];
const ALL_KEYS = new Set<string>([...LOOKUP_TAB_KEYS, ...CONFIG_TAB_KEYS]);

/** Unified Lookups + Configuration hub — sidebar deep-links via `?tab=`. */
export function PlatformMastersPage() {
  const [params, setParams] = useSearchParams();
  const rawTab = params.get("tab");
  const tab = rawTab ?? "banks";
  const isLookup = (LOOKUP_TAB_KEYS as readonly string[]).includes(tab);

  const selectTab = (key: string) => {
    setParams({ tab: key }, { replace: true });
  };

  const subtitle = useMemo(
    () =>
      isLookup
        ? "Lookups, lists, and organisation reference data."
        : "Platform settings, currencies, tax, payment terms, and reference masters.",
    [isLookup],
  );

  if (!rawTab || !ALL_KEYS.has(tab)) {
    return <Navigate to="/masters?tab=banks" replace />;
  }

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Masters</h1>
          <p>{subtitle}</p>
        </div>
        <div className="spacer" />
        <div className="viewtoggle">
          {ALL_TABS.map(({ key, icon, label }) => (
            <button
              key={key}
              type="button"
              className={tab === key ? "on" : ""}
              onClick={() => selectTab(key)}
            >
              <Icon name={icon} size={14} /> {label}
            </button>
          ))}
        </div>
      </div>

      {isLookup ? (
        <LookupsPage initialTab={tab} embedded />
      ) : (
        <ConfigurationPage initialTab={tab} embedded />
      )}
    </>
  );
}
