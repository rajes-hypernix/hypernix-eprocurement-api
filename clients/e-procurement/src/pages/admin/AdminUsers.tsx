import { useEffect, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  assignUserRoles,
  getUserById,
  getUserRoles,
  registerUser,
  searchUsers,
  toggleUserStatus,
  type UserDto,
  type UserRoleDto,
} from "@/api/identity";
import { Icon } from "@/components/Icon";
import { Gated } from "@/components/Gated";
import { EmptyState, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";
import { FshPermissions } from "@/lib/fsh-permissions";

function errMsg(e: unknown): string {
  if (e instanceof ApiRequestError) return e.problem?.detail ?? e.message;
  if (e instanceof Error) return e.message;
  return "Something went wrong.";
}

function displayName(u: UserDto): string {
  const n = [u.firstName, u.lastName].filter(Boolean).join(" ").trim();
  return n || u.userName || u.email || u.id || "User";
}

export function UsersListPage({ onNavigate }: { onNavigate: (path: string) => void }) {
  const [q, setQ] = useState("");
  const [search, setSearch] = useState("");
  const [active, setActive] = useState<"all" | "yes" | "no">("all");
  const [page, setPage] = useState(1);
  const pageSize = 20;

  useEffect(() => {
    const t = setTimeout(() => {
      setSearch(q);
      setPage(1);
    }, 250);
    return () => clearTimeout(t);
  }, [q]);

  const { data, isPending, isFetching } = useQuery({
    queryKey: ["users", search, active, page, pageSize],
    queryFn: () =>
      searchUsers({
        search: search.trim() || undefined,
        pageNumber: page,
        pageSize,
        isActive: active === "all" ? undefined : active === "yes",
      }),
  });

  const users = data?.items ?? [];
  const totalPages = data?.totalPages ?? 1;

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>User management</h1>
          <p>Directory of e-procurement users, roles, and access.</p>
        </div>
        <div className="spacer" />
        <button type="button" className="btn btn-out btn-sm" onClick={() => onNavigate("roles")}>
          <Icon name="users" size={15} /> Roles
        </button>
        <Gated permission={FshPermissions.users.create}>
          <button type="button" className="btn btn-pri btn-sm" onClick={() => onNavigate("new")}>
            <Icon name="plus" size={15} /> New user
          </button>
        </Gated>
      </div>

      <div className="ribbon">
        {data?.totalCount ?? 0} user(s)
        {isFetching && !isPending ? " · refreshing…" : null}
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0 }}>
              <label>Search name / email / username</label>
              <input
                value={q}
                placeholder="Search…"
                onChange={(e) => setQ(e.target.value)}
              />
            </div>
            <div className="field" style={{ margin: 0, minWidth: 160 }}>
              <label>Active</label>
              <select
                value={active}
                onChange={(e) => {
                  setActive(e.target.value as "all" | "yes" | "no");
                  setPage(1);
                }}
              >
                <option value="all">All</option>
                <option value="yes">Active</option>
                <option value="no">Inactive</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        {isPending ? (
          <Spinner label="Loading users…" />
        ) : (
          <>
            <table>
              <thead>
                <tr>
                  <th>User</th>
                  <th>Username</th>
                  <th>Email confirmed</th>
                  <th>Status</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr
                    key={u.id}
                    className="drillrow"
                    onClick={() => u.id && onNavigate(u.id)}
                  >
                    <td>
                      <div style={{ fontWeight: 700 }}>{displayName(u)}</div>
                    </td>
                    <td>{u.userName}</td>
                    <td>{u.emailConfirmed ? "Yes" : "No"}</td>
                    <td>
                      <span className={`badge ${u.isActive ? "b-green" : "b-grey"}`}>
                        {u.isActive ? "Active" : "Inactive"}
                      </span>
                    </td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        Open <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
                {users.length === 0 ? (
                  <tr>
                    <td colSpan={5}>
                      <EmptyState>No users match the filter.</EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
            {totalPages > 1 ? (
              <div className="actionbar" style={{ padding: "12px 16px" }}>
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={page <= 1}
                  onClick={() => setPage((p) => Math.max(1, p - 1))}
                >
                  Previous
                </button>
                <span className="hint" style={{ margin: "0 12px" }}>
                  Page {page} of {totalPages}
                </span>
                <button
                  type="button"
                  className="btn btn-out btn-sm"
                  disabled={page >= totalPages}
                  onClick={() => setPage((p) => p + 1)}
                >
                  Next
                </button>
              </div>
            ) : null}
          </>
        )}
      </div>
    </>
  );
}

export function UserCreatePage({
  onBack,
  onSaved,
}: {
  onBack: () => void;
  onSaved: (userId: string) => void;
}) {
  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [email, setEmail] = useState("");
  const [userName, setUserName] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [err, setErr] = useState<string | null>(null);

  const save = useMutation({
    mutationFn: () =>
      registerUser({ firstName, lastName, email, userName, password, confirmPassword }),
    onSuccess: (res) => onSaved(res.userId),
    onError: (e) => setErr(errMsg(e)),
  });

  const submit = () => {
    setErr(null);
    if (!firstName.trim() || !lastName.trim() || !email.trim() || !userName.trim()) {
      setErr("Fill in all required fields.");
      return;
    }
    if (password !== confirmPassword) {
      setErr("Passwords do not match.");
      return;
    }
    save.mutate();
  };

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Users
        </button>{" "}
        <Icon name="chev" size={12} /> New user
      </div>
      <div className="pagehead">
        <div>
          <h1>New user</h1>
          <p>Register a new e-procurement account.</p>
        </div>
      </div>

      {err ? <Notice tone="error">{err}</Notice> : null}

      <div className="card">
        <div className="cbody">
          <div className="grid2">
            <div className="field">
              <label>First name</label>
              <input value={firstName} onChange={(e) => setFirstName(e.target.value)} />
            </div>
            <div className="field">
              <label>Last name</label>
              <input value={lastName} onChange={(e) => setLastName(e.target.value)} />
            </div>
            <div className="field">
              <label>Email</label>
              <input type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
            </div>
            <div className="field">
              <label>Username</label>
              <input value={userName} onChange={(e) => setUserName(e.target.value)} />
            </div>
            <div className="field">
              <label>Password</label>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
              />
            </div>
            <div className="field">
              <label>Confirm password</label>
              <input
                type="password"
                value={confirmPassword}
                onChange={(e) => setConfirmPassword(e.target.value)}
              />
            </div>
          </div>
          <div className="actionbar" style={{ marginTop: 16 }}>
            <button type="button" className="btn btn-out" onClick={onBack}>
              Cancel
            </button>
            <button
              type="button"
              className="btn btn-pri"
              disabled={save.isPending}
              onClick={submit}
            >
              {save.isPending ? "Creating…" : "Create user"}
            </button>
          </div>
        </div>
      </div>
    </>
  );
}

export function UserDetailPage({
  userId,
  onBack,
}: {
  userId: string;
  onBack: () => void;
}) {
  const qc = useQueryClient();
  const [err, setErr] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);

  const userQuery = useQuery({
    queryKey: ["user", userId],
    queryFn: () => getUserById(userId),
  });

  const rolesQuery = useQuery({
    queryKey: ["user", userId, "roles"],
    queryFn: () => getUserRoles(userId),
  });

  const [roleDraft, setRoleDraft] = useState<UserRoleDto[]>([]);

  useEffect(() => {
    if (rolesQuery.data) setRoleDraft(rolesQuery.data.map((r) => ({ ...r })));
  }, [rolesQuery.data]);

  const toggleStatus = useMutation({
    mutationFn: (activate: boolean) => toggleUserStatus(userId, activate),
    onSuccess: () => {
      setErr(null);
      void qc.invalidateQueries({ queryKey: ["user", userId] });
      void qc.invalidateQueries({ queryKey: ["users"] });
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const saveRoles = useMutation({
    mutationFn: () => assignUserRoles(userId, roleDraft),
    onSuccess: () => {
      setErr(null);
      setSaved(true);
      void qc.invalidateQueries({ queryKey: ["user", userId, "roles"] });
      setTimeout(() => setSaved(false), 2000);
    },
    onError: (e) => setErr(errMsg(e)),
  });

  const user = userQuery.data;
  const name = user ? displayName(user) : userId;

  if (userQuery.isPending) return <Spinner label="Loading user…" />;

  return (
    <>
      <div className="crumb">
        <button type="button" className="lnk" onClick={onBack}>
          Users
        </button>{" "}
        <Icon name="chev" size={12} /> {name}
      </div>
      <div className="pagehead">
        <div>
          <h1>{name}</h1>
          <p>{user?.email}</p>
        </div>
        <div className="spacer" />
        <Gated permission={FshPermissions.users.update}>
          <button
            type="button"
            className={`btn btn-sm ${user?.isActive ? "btn-out" : "btn-pri"}`}
            disabled={toggleStatus.isPending}
            onClick={() => toggleStatus.mutate(!user?.isActive)}
          >
            {user?.isActive ? "Deactivate" : "Activate"}
          </button>
        </Gated>
      </div>

      {err ? <Notice tone="error">{err}</Notice> : null}
      {saved ? <Notice tone="success">Roles saved.</Notice> : null}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="grid2">
            <div>
              <div className="hint">Username</div>
              <div>{user?.userName}</div>
            </div>
            <div>
              <div className="hint">Status</div>
              <span className={`badge ${user?.isActive ? "b-green" : "b-grey"}`}>
                {user?.isActive ? "Active" : "Inactive"}
              </span>
            </div>
            <div>
              <div className="hint">Email confirmed</div>
              <div>{user?.emailConfirmed ? "Yes" : "No"}</div>
            </div>
          </div>
        </div>
      </div>

      <div className="card">
        <div className="chead">
          <h3>Roles</h3>
        </div>
        <div className="cbody">
          {rolesQuery.isPending ? (
            <Spinner label="Loading roles…" />
          ) : (
            <>
              {roleDraft.map((r, i) => (
                <label key={r.roleId ?? i} style={{ display: "flex", gap: 10, marginBottom: 10 }}>
                  <Gated permission={FshPermissions.users.manageRoles} fallback={
                    <input type="checkbox" checked={r.enabled} readOnly disabled />
                  }>
                    <input
                      type="checkbox"
                      checked={r.enabled}
                      onChange={(e) =>
                        setRoleDraft((prev) =>
                          prev.map((x, j) => (j === i ? { ...x, enabled: e.target.checked } : x)),
                        )
                      }
                    />
                  </Gated>
                  <span>
                    <strong>{r.roleName}</strong>
                    {r.description ? <span className="hint"> — {r.description}</span> : null}
                  </span>
                </label>
              ))}
              {roleDraft.length === 0 ? <EmptyState>No roles in catalog.</EmptyState> : null}
              <Gated permission={FshPermissions.users.manageRoles}>
                <div className="actionbar" style={{ marginTop: 12 }}>
                  <button
                    type="button"
                    className="btn btn-pri btn-sm"
                    disabled={saveRoles.isPending}
                    onClick={() => saveRoles.mutate()}
                  >
                    {saveRoles.isPending ? "Saving…" : "Save roles"}
                  </button>
                </div>
              </Gated>
            </>
          )}
        </div>
      </div>
    </>
  );
}
