import { useCallback, useEffect, useId, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { searchGlobal, type SearchHitDto } from "@/api/search";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { groupSearchHits, searchHitPath } from "@/lib/search-links";
import { pushRecent } from "@/lib/recents";

const DEBOUNCE_MS = 280;

export function GlobalSearch() {
  const { isVendor } = useAuth();
  const navigate = useNavigate();
  const listboxId = useId();
  const wrapRef = useRef<HTMLDivElement>(null);
  const [query, setQuery] = useState("");
  const [hits, setHits] = useState<SearchHitDto[]>([]);
  const [open, setOpen] = useState(false);
  const [loading, setLoading] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);

  const flatHits = groupSearchHits(hits).flatMap((g) => g.items);

  const goToHit = useCallback(
    (hit: SearchHitDto) => {
      const path = searchHitPath(hit, isVendor);
      pushRecent({
        path,
        label: hit.title,
        code: hit.code,
      });
      setOpen(false);
      setQuery("");
      setHits([]);
      void navigate(path);
    },
    [isVendor, navigate],
  );

  useEffect(() => {
    const trimmed = query.trim();
    if (trimmed.length < 2) {
      setHits([]);
      setLoading(false);
      setActiveIndex(-1);
      return;
    }

    setLoading(true);
    const timer = window.setTimeout(() => {
      void searchGlobal(trimmed)
        .then((results) => {
          setHits(results);
          setOpen(true);
          setActiveIndex(results.length > 0 ? 0 : -1);
        })
        .catch(() => {
          setHits([]);
          setOpen(true);
        })
        .finally(() => setLoading(false));
    }, DEBOUNCE_MS);

    return () => window.clearTimeout(timer);
  }, [query]);

  useEffect(() => {
    const onDocClick = (e: MouseEvent) => {
      if (!wrapRef.current?.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("mousedown", onDocClick);
    return () => document.removeEventListener("mousedown", onDocClick);
  }, []);

  const onKeyDown = (e: React.KeyboardEvent<HTMLInputElement>) => {
    if (!open || flatHits.length === 0) {
      if (e.key === "Escape") setOpen(false);
      return;
    }
    if (e.key === "ArrowDown") {
      e.preventDefault();
      setActiveIndex((i) => (i + 1) % flatHits.length);
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      setActiveIndex((i) => (i <= 0 ? flatHits.length - 1 : i - 1));
    } else if (e.key === "Enter" && activeIndex >= 0) {
      e.preventDefault();
      goToHit(flatHits[activeIndex]!);
    } else if (e.key === "Escape") {
      setOpen(false);
    }
  };

  const groups = groupSearchHits(hits);
  let rowIndex = -1;

  return (
    <div className="gsearch" ref={wrapRef}>
      <Icon name="eye" size={15} />
      <input
        type="search"
        role="combobox"
        aria-expanded={open}
        aria-controls={listboxId}
        aria-autocomplete="list"
        placeholder="Search vendors, reqs, RFQs, POs…"
        value={query}
        onChange={(e) => {
          setQuery(e.target.value);
          setOpen(true);
        }}
        onFocus={() => {
          if (query.trim().length >= 2) setOpen(true);
        }}
        onKeyDown={onKeyDown}
      />
      {loading ? (
        <span style={{ fontSize: 11, opacity: 0.7 }} aria-hidden="true">
          …
        </span>
      ) : null}
      {open && query.trim().length >= 2 ? (
        <div className="gsr" id={listboxId} role="listbox">
          {groups.length === 0 ? (
            <div className="gsr-empty muted">No matches for “{query.trim()}”</div>
          ) : (
            groups.map((group) => (
              <div key={group.type}>
                <div className="gsr-group">{group.label}</div>
                {group.items.map((hit) => {
                  rowIndex += 1;
                  const idx = rowIndex;
                  return (
                    <button
                      key={`${hit.type}-${hit.id}`}
                      type="button"
                      role="option"
                      aria-selected={idx === activeIndex}
                      className={`gsr-item${idx === activeIndex ? " on" : ""}`}
                      onMouseEnter={() => setActiveIndex(idx)}
                      onClick={() => goToHit(hit)}
                    >
                      <strong>{hit.code}</strong>
                      <span className="gsr-title">
                        {hit.title}
                        {hit.subtitle ? ` · ${hit.subtitle}` : ""}
                      </span>
                    </button>
                  );
                })}
              </div>
            ))
          )}
        </div>
      ) : null}
    </div>
  );
}
