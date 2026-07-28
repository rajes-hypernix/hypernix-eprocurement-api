import { useState, type FormEvent } from "react";
import { Link, Navigate, useLocation, useNavigate } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { AmbiguousTenantError, type ResolveTenantCandidate } from "@/auth/api";
import { tokenStore } from "@/auth/token-store";
import { ApiRequestError } from "@/lib/api-client";
import {
  AuthEyeIcon,
  AuthLockIcon,
  AuthSplitShell,
  AuthUserIcon,
} from "@/components/auth/AuthSplitShell";

type LocationState = { from?: { pathname: string }; passwordReset?: boolean };

/** Split-card login: email + password only; tenant resolved by API from email. */
export function LoginPage() {
  const { isAuthenticated, isInitializing, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const state = location.state as LocationState | null;
  const from = state?.from?.pathname ?? "/dashboard";

  const [email, setEmail] = useState(() => tokenStore.getRememberedEmail() ?? "");
  const [password, setPassword] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe, setRememberMe] = useState(() => tokenStore.getRememberMe());
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [success, setSuccess] = useState<string | null>(
    state?.passwordReset ? "Password updated. Sign in with your new password." : null,
  );
  const [candidates, setCandidates] = useState<ResolveTenantCandidate[] | null>(null);
  const [selectedTenant, setSelectedTenant] = useState<string | null>(null);

  if (isInitializing) {
    return (
      <div className="login-page" role="status">
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
    setSuccess(null);
    setSubmitting(true);
    try {
      await login({
        email,
        password,
        tenant: selectedTenant ?? undefined,
        rememberMe,
      });
      navigate(from, { replace: true });
    } catch (err) {
      if (err instanceof AmbiguousTenantError) {
        setCandidates(err.candidates);
        setSelectedTenant(err.candidates[0]?.tenantId ?? null);
        setError(err.message);
      } else {
        const message =
          err instanceof ApiRequestError
            ? (err.problem?.detail ?? err.problem?.title ?? err.message)
            : err instanceof Error
              ? err.message
              : "Login failed";
        setError(message);
        setCandidates(null);
        setSelectedTenant(null);
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthSplitShell>
      <h2 className="login-welcome">Welcome back</h2>
      <p className="login-welcome-sub">
        Sign <strong>in</strong> to continue to Hypernix eProcure.
      </p>

      <form onSubmit={(e) => void onSubmit(e)}>
        <div className="login-field">
          <label htmlFor="login-email">Username or Email</label>
          <div className="login-input-wrap">
            <span className="login-input-icon">
              <AuthUserIcon />
            </span>
            <input
              id="login-email"
              type="email"
              value={email}
              onChange={(e) => {
                setEmail(e.target.value);
                setCandidates(null);
                setSelectedTenant(null);
              }}
              placeholder="you@company.com"
              autoComplete="username"
              required
            />
          </div>
        </div>

        <div className="login-field">
          <label htmlFor="login-password">Password</label>
          <div className="login-input-wrap">
            <span className="login-input-icon">
              <AuthLockIcon />
            </span>
            <input
              id="login-password"
              className="login-has-toggle"
              type={showPassword ? "text" : "password"}
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              placeholder="Enter your password"
              autoComplete="current-password"
              required
            />
            <button
              type="button"
              className="login-eye"
              aria-label={showPassword ? "Hide password" : "Show password"}
              onClick={() => setShowPassword((v) => !v)}
            >
              <AuthEyeIcon off={showPassword} />
            </button>
          </div>
        </div>

        <label className="login-remember">
          <input
            type="checkbox"
            checked={rememberMe}
            onChange={(e) => setRememberMe(e.target.checked)}
          />
          <span>Remember me</span>
        </label>

        {candidates && candidates.length > 0 ? (
          <div className="login-candidates" role="listbox" aria-label="Choose organization">
            {candidates.map((c) => (
              <button
                key={c.tenantId}
                type="button"
                role="option"
                aria-selected={selectedTenant === c.tenantId}
                className={`login-candidate${selectedTenant === c.tenantId ? " is-selected" : ""}`}
                onClick={() => setSelectedTenant(c.tenantId)}
              >
                {c.tenantName || c.tenantId}
              </button>
            ))}
          </div>
        ) : null}

        {success ? (
          <p className="login-success" role="status">
            {success}
          </p>
        ) : null}

        {error ? (
          <p className="login-error" role="alert">
            {error}
          </p>
        ) : null}

        <button type="submit" className="login-submit" disabled={submitting}>
          {submitting ? "Signing in…" : "Sign in"}
        </button>

        <Link to="/forgot-password" className="login-forgot is-link">
          Forgot password?
        </Link>
      </form>
    </AuthSplitShell>
  );
}
