import { useEffect, useState, type FormEvent } from "react";
import { Link, Navigate, useNavigate, useSearchParams } from "react-router-dom";
import { useAuth } from "@/auth/use-auth";
import { resetPassword } from "@/auth/api";
import { ApiRequestError } from "@/lib/api-client";
import { AuthEyeIcon, AuthLockIcon, AuthSplitShell } from "@/components/auth/AuthSplitShell";

/**
 * Reset password — landed from email:
 *   /reset-password?token=…&email=…&tenant=…
 */
export function ResetPasswordPage() {
  const { isAuthenticated, isInitializing } = useAuth();
  const navigate = useNavigate();
  const [params] = useSearchParams();

  const token = params.get("token") ?? "";
  const email = params.get("email") ?? "";
  const tenant = params.get("tenant") ?? "";

  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [showPassword, setShowPassword] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const matches = password.length > 0 && password === confirm;
  const malformed = !token || !email || !tenant;

  useEffect(() => {
    setError(null);
  }, [password, confirm]);

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

  const onSubmit = async (e: FormEvent) => {
    e.preventDefault();
    if (!matches) {
      setError("Passwords don't match.");
      return;
    }
    if (password.length < 8) {
      setError("Use at least 8 characters.");
      return;
    }

    setError(null);
    setSubmitting(true);
    try {
      await resetPassword({ email, password, token, tenant });
      navigate("/login", { replace: true, state: { passwordReset: true } });
    } catch (err) {
      const message =
        err instanceof ApiRequestError
          ? (err.problem?.detail ?? err.problem?.title ?? err.message)
          : err instanceof Error
            ? err.message
            : "Could not reset password";
      setError(message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <AuthSplitShell>
      {malformed ? (
        <>
          <h2 className="login-welcome">This link is incomplete</h2>
          <p className="login-welcome-sub">
            The reset link is missing token, email, or tenant. Some email clients clip long URLs —
            try pasting the full link from the email into your browser.
          </p>
          <Link to="/forgot-password" className="login-submit" style={{ display: "block", textAlign: "center", textDecoration: "none" }}>
            Request a new link
          </Link>
          <Link to="/login" className="login-forgot is-link">
            Back to sign in
          </Link>
        </>
      ) : (
        <>
          <h2 className="login-welcome">Set a new password</h2>
          <p className="login-welcome-sub">
            Resetting password for <strong>{email}</strong>.
          </p>

          <form onSubmit={(e) => void onSubmit(e)}>
            <div className="login-field">
              <label htmlFor="reset-password">New password</label>
              <div className="login-input-wrap">
                <span className="login-input-icon">
                  <AuthLockIcon />
                </span>
                <input
                  id="reset-password"
                  className="login-has-toggle"
                  type={showPassword ? "text" : "password"}
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  placeholder="At least 8 characters"
                  autoComplete="new-password"
                  minLength={8}
                  required
                  autoFocus
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

            <div className="login-field">
              <label htmlFor="reset-confirm">Confirm password</label>
              <div className="login-input-wrap">
                <span className="login-input-icon">
                  <AuthLockIcon />
                </span>
                <input
                  id="reset-confirm"
                  className="login-has-toggle"
                  type={showConfirm ? "text" : "password"}
                  value={confirm}
                  onChange={(e) => setConfirm(e.target.value)}
                  placeholder="Re-enter password"
                  autoComplete="new-password"
                  minLength={8}
                  required
                />
                <button
                  type="button"
                  className="login-eye"
                  aria-label={showConfirm ? "Hide password" : "Show password"}
                  onClick={() => setShowConfirm((v) => !v)}
                >
                  <AuthEyeIcon off={showConfirm} />
                </button>
              </div>
              {confirm.length > 0 ? (
                <p className={`login-hint ${matches ? "is-ok" : ""}`}>
                  {matches ? "Passwords match" : "Doesn't match yet"}
                </p>
              ) : null}
            </div>

            {error ? (
              <p className="login-error" role="alert">
                {error}
              </p>
            ) : null}

            <button
              type="submit"
              className="login-submit"
              disabled={submitting || !matches || password.length < 8}
            >
              {submitting ? "Updating password…" : "Set new password"}
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
