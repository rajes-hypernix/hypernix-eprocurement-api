import { useState, type FormEvent } from "react";
import { Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { ApiRequestError } from "@/lib/api-client";
import { env } from "@/env";

type LocationState = { from?: { pathname: string } };

/** Login styled with original eProcure tokens/classes (not FSH admin chrome). */
export function LoginPage() {
  const { isAuthenticated, isInitializing, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const from = (location.state as LocationState | null)?.from?.pathname ?? "/dashboard";

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [tenant, setTenant] = useState(env.defaultTenant);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (isInitializing) {
    return (
      <div className="shell" style={{ placeItems: "center", minHeight: "100vh" }} role="status">
        <p className="muted">Restoring your session…</p>
      </div>
    );
  }

  if (isAuthenticated) {
    return <Navigate to={from} replace />;
  }

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      await login({ email, password, tenant });
      navigate(from, { replace: true });
    } catch (err) {
      const message =
        err instanceof ApiRequestError
          ? (err.problem?.detail ?? err.problem?.title ?? err.message)
          : err instanceof Error
            ? err.message
            : "Login failed";
      setError(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      style={{
        minHeight: "100vh",
        display: "grid",
        placeItems: "center",
        padding: 24,
        background: "var(--cream)",
      }}
    >
      <div className="card" style={{ width: "100%", maxWidth: 420 }}>
        <div className="cbody">
          <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 18 }}>
            <span className="logo" style={{ background: "var(--teal)", width: 36, height: 36 }}>
              <svg
                width="18"
                height="18"
                viewBox="0 0 24 24"
                fill="none"
                stroke="#fff"
                strokeWidth={2.1}
                strokeLinecap="round"
                strokeLinejoin="round"
                aria-hidden="true"
              >
                <path d="M3 7l9-4 9 4-9 4-9-4z" />
                <path d="M3 7v10l9 4 9-4V7" />
                <path d="M12 11v10" />
              </svg>
            </span>
            <div>
              <div style={{ fontWeight: 700, fontSize: 18 }}>Hypernix eProcure</div>
              <div className="muted">Sign in to continue</div>
            </div>
          </div>

          <form onSubmit={(e) => void onSubmit(e)} style={{ display: "grid", gap: 12 }}>
            <label className="field">
              <span className="hint">Tenant</span>
              <input
                value={tenant}
                onChange={(e) => setTenant(e.target.value)}
                autoComplete="organization"
                required
              />
            </label>
            <label className="field">
              <span className="hint">Email</span>
              <input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoComplete="username"
                required
              />
            </label>
            <label className="field">
              <span className="hint">Password</span>
              <input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                autoComplete="current-password"
                required
              />
            </label>

            {error ? (
              <p className="muted" role="alert" style={{ color: "var(--red)", margin: 0 }}>
                {error}
              </p>
            ) : null}

            <button type="submit" className="btn primary" disabled={submitting}>
              {submitting ? "Signing in…" : "Sign in"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
