import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createBank,
  createCity,
  createCountry,
  createCustomList,
  createOrgUnit,
  createState,
  listBanks,
  listCities,
  listCountries,
  listCustomListItems,
  listCustomLists,
  listOrgUnits,
  listStates,
  setBankActive,
  setCountryActive,
  setOrgUnitActive,
  upsertCustomListItem,
  type BankDto,
  type CountryDto,
  type CustomListDto,
  type OrgUnitDto,
  type StateDto,
} from "@/api/platform";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { EmptyState, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";
import { FshPermissions } from "@/lib/fsh-permissions";

type Tab = "lists" | "countries" | "banks" | "org";

function errMsg(e: unknown): string {
  if (e instanceof ApiRequestError) return e.problem?.detail ?? e.message;
  if (e instanceof Error) return e.message;
  return "Something went wrong.";
}

export function LookupsPage() {
  const [tab, setTab] = useState<Tab>("lists");

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Lookups &amp; lists</h1>
          <p>Custom lists, geography, banks, and organisation units.</p>
        </div>
        <div className="spacer" />
        <div className="viewtoggle">
          {(
            [
              ["lists", "list", "Lists"],
              ["countries", "field", "Countries"],
              ["banks", "clip", "Banks"],
              ["org", "users", "Org"],
            ] as const
          ).map(([key, icon, label]) => (
            <button
              key={key}
              type="button"
              className={tab === key ? "on" : ""}
              onClick={() => setTab(key)}
            >
              <Icon name={icon} size={14} /> {label}
            </button>
          ))}
        </div>
      </div>

      {tab === "lists" ? <ListsTab /> : null}
      {tab === "countries" ? <CountriesTab /> : null}
      {tab === "banks" ? <BanksTab /> : null}
      {tab === "org" ? <OrgTab /> : null}
    </>
  );
}

function ListsTab() {
  const qc = useQueryClient();
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
  const [err, setErr] = useState<string | null>(null);
  const [newListKey, setNewListKey] = useState("");
  const [newListName, setNewListName] = useState("");
  const [itemCode, setItemCode] = useState("");
  const [itemLabel, setItemLabel] = useState("");
  const [itemSort, setItemSort] = useState("0");

  const listsQuery = useQuery({
    queryKey: ["custom-lists", false],
    queryFn: () => listCustomLists(false),
  });

  const itemsQuery = useQuery({
    queryKey: ["custom-list-items", selectedKey],
    queryFn: () => listCustomListItems(selectedKey!, false),
    enabled: !!selectedKey,
  });

  const createList = useMutation({
    mutationFn: () => createCustomList({ key: newListKey.trim(), name: newListName.trim() }),
    onSuccess: () => {
      setErr(null);
      setNewListKey("");
      setNewListName("");
      void qc.invalidateQueries({ queryKey: ["custom-lists"] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const upsertItem = useMutation({
    mutationFn: () =>
      upsertCustomListItem(selectedKey!, {
        code: itemCode.trim(),
        label: itemLabel.trim(),
        sortOrder: Number(itemSort) || 0,
      }),
    onSuccess: () => {
      setErr(null);
      setItemCode("");
      setItemLabel("");
      void qc.invalidateQueries({ queryKey: ["custom-list-items", selectedKey] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const lists = listsQuery.data ?? [];

  return (
    <>
      {err ? <Notice tone="error">{err}</Notice> : null}
      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <Gated permission={FshPermissions.customLists.manage}>
            <div className="filterbar">
              <div className="field" style={{ margin: 0 }}>
                <label>New list key</label>
                <input value={newListKey} onChange={(e) => setNewListKey(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Name</label>
                <input value={newListName} onChange={(e) => setNewListName(e.target.value)} />
              </div>
              <button
                type="button"
                className="btn btn-pri btn-sm"
                style={{ alignSelf: "flex-end" }}
                disabled={createList.isPending}
                onClick={() => createList.mutate()}
              >
                Create list
              </button>
            </div>
          </Gated>
        </div>
      </div>

      <div className="card">
        {listsQuery.isPending ? (
          <Spinner label="Loading lists…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Key</th>
                <th>Name</th>
                <th>Active</th>
              </tr>
            </thead>
            <tbody>
              {lists.map((l: CustomListDto) => (
                <tr
                  key={l.id}
                  className={`drillrow${selectedKey === l.key ? " on" : ""}`}
                  onClick={() => setSelectedKey(l.key)}
                >
                  <td style={{ fontWeight: 700 }}>{l.key}</td>
                  <td>{l.name}</td>
                  <td>{l.isActive ? "Yes" : "No"}</td>
                </tr>
              ))}
              {lists.length === 0 ? (
                <tr>
                  <td colSpan={3}>
                    <EmptyState>No custom lists.</EmptyState>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>

      {selectedKey ? (
        <div className="card" style={{ marginTop: 14 }}>
          <div className="chead">
            <h3>Items — {selectedKey}</h3>
          </div>
          <div className="cbody">
            <Gated permission={FshPermissions.customLists.manage}>
              <div className="filterbar" style={{ marginBottom: 14 }}>
                <div className="field" style={{ margin: 0 }}>
                  <label>Code</label>
                  <input value={itemCode} onChange={(e) => setItemCode(e.target.value)} />
                </div>
                <div className="field" style={{ margin: 0 }}>
                  <label>Label</label>
                  <input value={itemLabel} onChange={(e) => setItemLabel(e.target.value)} />
                </div>
                <div className="field" style={{ margin: 0, maxWidth: 80 }}>
                  <label>Sort</label>
                  <input value={itemSort} onChange={(e) => setItemSort(e.target.value)} />
                </div>
                <button
                  type="button"
                  className="btn btn-pri btn-sm"
                  style={{ alignSelf: "flex-end" }}
                  disabled={upsertItem.isPending}
                  onClick={() => upsertItem.mutate()}
                >
                  Upsert item
                </button>
              </div>
            </Gated>
            {itemsQuery.isPending ? (
              <Spinner label="Loading items…" />
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Label</th>
                    <th>Sort</th>
                    <th>Active</th>
                  </tr>
                </thead>
                <tbody>
                  {(itemsQuery.data ?? []).map((item) => (
                    <tr key={item.id}>
                      <td>{item.code}</td>
                      <td>{item.label}</td>
                      <td>{item.sortOrder}</td>
                      <td>{item.isActive ? "Yes" : "No"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </div>
      ) : null}
    </>
  );
}

function CountriesTab() {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [selectedCountry, setSelectedCountry] = useState<string | null>(null);
  const [selectedState, setSelectedState] = useState<string | null>(null);
  const [countryCode, setCountryCode] = useState("");
  const [countryName, setCountryName] = useState("");
  const [stateCode, setStateCode] = useState("");
  const [stateName, setStateName] = useState("");
  const [cityName, setCityName] = useState("");

  const countriesQuery = useQuery({
    queryKey: ["countries", false],
    queryFn: () => listCountries(false),
  });

  const statesQuery = useQuery({
    queryKey: ["states", selectedCountry],
    queryFn: () => listStates(selectedCountry!, false),
    enabled: !!selectedCountry,
  });

  const citiesQuery = useQuery({
    queryKey: ["cities", selectedState],
    queryFn: () => listCities(selectedState!, false),
    enabled: !!selectedState,
  });

  const addCountry = useMutation({
    mutationFn: () => createCountry({ code: countryCode.trim(), name: countryName.trim() }),
    onSuccess: () => {
      setErr(null);
      setCountryCode("");
      setCountryName("");
      void qc.invalidateQueries({ queryKey: ["countries"] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const toggleCountry = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setCountryActive(id, active),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["countries"] }),
    onError: (e) => setErr(errMsg(e)),
  });

  const addState = useMutation({
    mutationFn: () =>
      createState({
        countryId: selectedCountry!,
        code: stateCode.trim(),
        name: stateName.trim(),
      }),
    onSuccess: () => {
      setErr(null);
      setStateCode("");
      setStateName("");
      void qc.invalidateQueries({ queryKey: ["states", selectedCountry] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const addCity = useMutation({
    mutationFn: () => createCity({ stateId: selectedState!, name: cityName.trim() }),
    onSuccess: () => {
      setErr(null);
      setCityName("");
      void qc.invalidateQueries({ queryKey: ["cities", selectedState] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  return (
    <>
      {err ? <Notice tone="error">{err}</Notice> : null}
      <Gated permission={FshPermissions.lookups.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div className="filterbar">
              <div className="field" style={{ margin: 0 }}>
                <label>Country code</label>
                <input value={countryCode} onChange={(e) => setCountryCode(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Country name</label>
                <input value={countryName} onChange={(e) => setCountryName(e.target.value)} />
              </div>
              <button
                type="button"
                className="btn btn-pri btn-sm"
                style={{ alignSelf: "flex-end" }}
                disabled={addCountry.isPending}
                onClick={() => addCountry.mutate()}
              >
                Add country
              </button>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        {countriesQuery.isPending ? (
          <Spinner label="Loading countries…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Active</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {(countriesQuery.data ?? []).map((c: CountryDto) => (
                <tr
                  key={c.id}
                  className={`drillrow${selectedCountry === c.id ? " on" : ""}`}
                  onClick={() => {
                    setSelectedCountry(c.id);
                    setSelectedState(null);
                  }}
                >
                  <td style={{ fontWeight: 700 }}>{c.code}</td>
                  <td>{c.name}</td>
                  <td>{c.isActive ? "Yes" : "No"}</td>
                  <td className="amt" onClick={(e) => e.stopPropagation()}>
                    <Gated permission={FshPermissions.lookups.manage}>
                      <button
                        type="button"
                        className="btn btn-out btn-sm"
                        onClick={() => toggleCountry.mutate({ id: c.id, active: !c.isActive })}
                      >
                        {c.isActive ? "Deactivate" : "Activate"}
                      </button>
                    </Gated>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>

      {selectedCountry ? (
        <>
          <Gated permission={FshPermissions.lookups.manage}>
            <div className="card" style={{ marginTop: 14 }}>
              <div className="chead">
                <h3>States</h3>
              </div>
              <div className="cbody">
                <div className="filterbar" style={{ marginBottom: 14 }}>
                  <div className="field" style={{ margin: 0 }}>
                    <label>State code</label>
                    <input value={stateCode} onChange={(e) => setStateCode(e.target.value)} />
                  </div>
                  <div className="field" style={{ margin: 0 }}>
                    <label>State name</label>
                    <input value={stateName} onChange={(e) => setStateName(e.target.value)} />
                  </div>
                  <button
                    type="button"
                    className="btn btn-pri btn-sm"
                    style={{ alignSelf: "flex-end" }}
                    disabled={addState.isPending}
                    onClick={() => addState.mutate()}
                  >
                    Add state
                  </button>
                </div>
              </div>
            </div>
          </Gated>
          <div className="card">
            {statesQuery.isPending ? (
              <Spinner />
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>Code</th>
                    <th>Name</th>
                    <th>Active</th>
                  </tr>
                </thead>
                <tbody>
                  {(statesQuery.data ?? []).map((s: StateDto) => (
                    <tr
                      key={s.id}
                      className={`drillrow${selectedState === s.id ? " on" : ""}`}
                      onClick={() => setSelectedState(s.id)}
                    >
                      <td>{s.code}</td>
                      <td>{s.name}</td>
                      <td>{s.isActive ? "Yes" : "No"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      ) : null}

      {selectedState ? (
        <>
          <Gated permission={FshPermissions.lookups.manage}>
            <div className="card" style={{ marginTop: 14 }}>
              <div className="cbody">
                <div className="filterbar">
                  <div className="field" style={{ margin: 0 }}>
                    <label>City name</label>
                    <input value={cityName} onChange={(e) => setCityName(e.target.value)} />
                  </div>
                  <button
                    type="button"
                    className="btn btn-pri btn-sm"
                    style={{ alignSelf: "flex-end" }}
                    disabled={addCity.isPending}
                    onClick={() => addCity.mutate()}
                  >
                    Add city
                  </button>
                </div>
              </div>
            </div>
          </Gated>
          <div className="card">
            {citiesQuery.isPending ? (
              <Spinner />
            ) : (
              <table>
                <thead>
                  <tr>
                    <th>Name</th>
                    <th>Active</th>
                  </tr>
                </thead>
                <tbody>
                  {(citiesQuery.data ?? []).map((city) => (
                    <tr key={city.id}>
                      <td>{city.name}</td>
                      <td>{city.isActive ? "Yes" : "No"}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            )}
          </div>
        </>
      ) : null}
    </>
  );
}

function BanksTab() {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [name, setName] = useState("");
  const [countryCode, setCountryCode] = useState("");
  const [swiftCode, setSwiftCode] = useState("");

  const banksQuery = useQuery({
    queryKey: ["banks", false],
    queryFn: () => listBanks(undefined, false),
  });

  const addBank = useMutation({
    mutationFn: () =>
      createBank({
        name: name.trim(),
        countryCode: countryCode.trim(),
        swiftCode: swiftCode.trim() || undefined,
      }),
    onSuccess: () => {
      setErr(null);
      setName("");
      setCountryCode("");
      setSwiftCode("");
      void qc.invalidateQueries({ queryKey: ["banks"] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const toggleBank = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setBankActive(id, active),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["banks"] }),
    onError: (e) => setErr(errMsg(e)),
  });

  return (
    <>
      {err ? <Notice tone="error">{err}</Notice> : null}
      <Gated permission={FshPermissions.lookups.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div className="filterbar">
              <div className="field" style={{ margin: 0 }}>
                <label>Bank name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Country code</label>
                <input value={countryCode} onChange={(e) => setCountryCode(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>SWIFT (optional)</label>
                <input value={swiftCode} onChange={(e) => setSwiftCode(e.target.value)} />
              </div>
              <button
                type="button"
                className="btn btn-pri btn-sm"
                style={{ alignSelf: "flex-end" }}
                disabled={addBank.isPending}
                onClick={() => addBank.mutate()}
              >
                Add bank
              </button>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        {banksQuery.isPending ? (
          <Spinner label="Loading banks…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Name</th>
                <th>Country</th>
                <th>SWIFT</th>
                <th>Active</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {(banksQuery.data ?? []).map((b: BankDto) => (
                <tr key={b.id}>
                  <td style={{ fontWeight: 700 }}>{b.name}</td>
                  <td>{b.countryCode}</td>
                  <td>{b.swiftCode ?? "—"}</td>
                  <td>{b.isActive ? "Yes" : "No"}</td>
                  <td className="amt">
                    <Gated permission={FshPermissions.lookups.manage}>
                      <button
                        type="button"
                        className="btn btn-out btn-sm"
                        onClick={() => toggleBank.mutate({ id: b.id, active: !b.isActive })}
                      >
                        {b.isActive ? "Deactivate" : "Activate"}
                      </button>
                    </Gated>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}

function OrgTab() {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [code, setCode] = useState("");
  const [name, setName] = useState("");
  const [type, setType] = useState("");
  const [parentId, setParentId] = useState("");

  const orgQuery = useQuery({
    queryKey: ["org-units", false],
    queryFn: () => listOrgUnits(undefined, false),
  });

  const addOrg = useMutation({
    mutationFn: () =>
      createOrgUnit({
        code: code.trim(),
        name: name.trim(),
        type: type.trim(),
        parentId: parentId.trim() || null,
      }),
    onSuccess: () => {
      setErr(null);
      setCode("");
      setName("");
      setType("");
      setParentId("");
      void qc.invalidateQueries({ queryKey: ["org-units"] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const toggleOrg = useMutation({
    mutationFn: ({ id, active }: { id: string; active: boolean }) => setOrgUnitActive(id, active),
    onSuccess: () => void qc.invalidateQueries({ queryKey: ["org-units"] }),
    onError: (e) => setErr(errMsg(e)),
  });

  const units = orgQuery.data ?? [];

  return (
    <>
      {err ? <Notice tone="error">{err}</Notice> : null}
      <Gated permission={FshPermissions.org.manage}>
        <div className="card" style={{ marginBottom: 14 }}>
          <div className="cbody">
            <div className="filterbar">
              <div className="field" style={{ margin: 0 }}>
                <label>Code</label>
                <input value={code} onChange={(e) => setCode(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Name</label>
                <input value={name} onChange={(e) => setName(e.target.value)} />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Type</label>
                <input value={type} onChange={(e) => setType(e.target.value)} placeholder="e.g. Department" />
              </div>
              <div className="field" style={{ margin: 0 }}>
                <label>Parent (optional)</label>
                <select value={parentId} onChange={(e) => setParentId(e.target.value)}>
                  <option value="">None</option>
                  {units.map((u: OrgUnitDto) => (
                    <option key={u.id} value={u.id}>
                      {u.code} — {u.name}
                    </option>
                  ))}
                </select>
              </div>
              <button
                type="button"
                className="btn btn-pri btn-sm"
                style={{ alignSelf: "flex-end" }}
                disabled={addOrg.isPending}
                onClick={() => addOrg.mutate()}
              >
                Add org unit
              </button>
            </div>
          </div>
        </div>
      </Gated>

      <div className="card">
        {orgQuery.isPending ? (
          <Spinner label="Loading org units…" />
        ) : (
          <table>
            <thead>
              <tr>
                <th>Code</th>
                <th>Name</th>
                <th>Type</th>
                <th>Active</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {units.map((u: OrgUnitDto) => (
                <tr key={u.id}>
                  <td style={{ fontWeight: 700 }}>{u.code}</td>
                  <td>{u.name}</td>
                  <td>{u.type}</td>
                  <td>{u.isActive ? "Yes" : "No"}</td>
                  <td className="amt">
                    <Gated permission={FshPermissions.org.manage}>
                      <button
                        type="button"
                        className="btn btn-out btn-sm"
                        onClick={() => toggleOrg.mutate({ id: u.id, active: !u.isActive })}
                      >
                        {u.isActive ? "Deactivate" : "Activate"}
                      </button>
                    </Gated>
                  </td>
                </tr>
              ))}
              {units.length === 0 ? (
                <tr>
                  <td colSpan={5}>
                    <EmptyState>No organisation units.</EmptyState>
                  </td>
                </tr>
              ) : null}
            </tbody>
          </table>
        )}
      </div>
    </>
  );
}
