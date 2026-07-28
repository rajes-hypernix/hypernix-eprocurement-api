import { useState, type FormEvent } from "react";
import { Link, Navigate } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import {
  AmbiguousTenantError,
  requestPasswordReset,
  resolveTenantByEmail,
  type ResolveTenantCandidate,
} from "@/auth/api";
import { ApiRequestError } from "@/lib/api-client";
import { AuthMailIcon, AuthSplitShell } from "@/components/auth/AuthSplitShell";

/**
 * Forgot password — email only → resolve tenant → request reset email.
 * Always shows the same success copy after 2xx (anti-enumeration).
 */
export function ForgotPasswordPage() {
  const { isAuthenticated, isInitializing } = useAuth();
  const [email, setEmail] = useState("");
  const [submitting, setSubmitting] = useState(false);
  const [submitted, setSubmitted] = useState(false);
  const [error, setError] = useState<string | null>(null);
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
    return <Navigate to="/dashboard" replace />;
  }

  const sendReset = async (tenantId: string) => {
    await requestPasswordReset({ email, tenant: tenantId });
    setSubmitted(true);
    setCandidates(null);
  };

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setError(null);
    setSubmitting(true);
    try {
      if (selectedTenant) {
        await sendReset(selectedTenant);
        return;
      }

      const resolved = await resolveTenantByEmail(email);
      const tenantId = resolved.tenantId;
      if (!tenantId) {
        // Unknown email — still show success (do not leak existence).
        // Soft-call forgot with default tenant so behaviour matches known-user path timing-wise is optional;
        // we simply show the inbox message without hitting the API when resolve returns empty.
        setSubmitted(true);
        return;
      }
      await sendReset(tenantId);
    } catch (err) {
      if (err instanceof AmbiguousTenantError) {
        setCandidates(err.candidates);
        setSelectedTenant(err.candidates[0]?.tenantId ?? null);
        setError(err.message);
      } else if (err instanceof ApiRequestError && err.status === 404) {
        // Unknown email — uniform success
        setSubmitted(true);
      } else {
        const message =
          err instanceof ApiRequestError
            ? (err.problem?.detail ?? err.problem?.title ?? err.message)
            : err instanceof Error
              ? err.message
              : "Could not send reset link";
        setError(message);
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthSplitShell>
      {submitted ? (
        <>
          <h2 className="login-welcome">Check your inbox</h2>
          <p className="login-welcome-sub">
            If an account exists for <strong>{email}</strong>, a one-time reset link is on its way.
            The link expires soon — check spam if you don&apos;t see it.
          </p>
          <button
            type="button"
            className="login-submit"
            onClick={() => {
              setSubmitted(false);
              setError(null);
              setCandidates(null);
              setSelectedTenant(null);
            }}
          >
            Try a different address
          </button>
          <Link to="/login" className="login-forgot is-link">
            Back to sign in
          </Link>
        </>
      ) : (
        <>
          <h2 className="login-welcome">Reset your password</h2>
          <p className="login-welcome-sub">
            Enter the email you sign in with. We&apos;ll send a one-time link.
          </p>

          <form onSubmit={(e) => void onSubmit(e)}>
            <div className="login-field">
              <label htmlFor="forgot-email">Email</label>
              <div className="login-input-wrap">
                <span className="login-input-icon">
                  <AuthMailIcon />
                </span>
                <input
                  id="forgot-email"
                  type="email"
                  value={email}
                  onChange={(e) => {
                    setEmail(e.target.value);
                    setCandidates(null);
                    setSelectedTenant(null);
                  }}
                  placeholder="you@company.com"
                  autoComplete="email"
                  required
                  autoFocus
                />
              </div>
            </div>

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

            {error ? (
              <p className="login-error" role="alert">
                {error}
              </p>
            ) : null}

            <button type="submit" className="login-submit" disabled={submitting || !email}>
              {submitting ? "Sending link…" : selectedTenant ? "Send reset link" : "Continue"}
            </button>

            <Link to="/login" className="login-forgot is-link">
              Back to sign in
            </Link>
          </form>
        </>
      )}
    </AuthSplitShell>
  );
}
