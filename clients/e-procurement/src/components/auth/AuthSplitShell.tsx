import type { ReactNode } from "react";
import "@/pages/login.css";

/** Shared split-card chrome for login / forgot / reset password. */
export function AuthSplitShell({ children }: { children: ReactNode }) {
  return (
    <div className="login-page">
      <div className="login-card">
        <aside className="login-pane-brand">
          <div className="login-orb login-orb-1" aria-hidden="true" />
          <div className="login-orb login-orb-2" aria-hidden="true" />
          <div className="login-orb login-orb-3" aria-hidden="true" />

          <div className="login-brand-lockup">
            <img src="/logos/hypernix-brand.png" alt="Hypernix" />
            <span className="login-brand-product">eProcure</span>
          </div>

          <div>
            <h1 className="login-tagline">
              Procurement,
              <br />
              connected.
            </h1>
            <div className="login-accent" />
            <p className="login-desc">
              One modern workspace for procurement, sourcing, vendor management and supply chain
              collaboration.
            </p>
          </div>

          <div className="login-brand-foot">
            <div className="login-partners">
              <div className="login-partner">
                <div className="login-partner-pill">
                  <img src="/logos/netsuite-pill.png" alt="Oracle NetSuite" />
                </div>
              </div>
              <div className="login-partner-div" aria-hidden="true" />
              <div className="login-partner">
                <div className="login-partner-circle">
                  <img src="/logos/hypernix-mark.svg" alt="" />
                </div>
                <div className="login-partner-copy">
                  <h4>HX eProcure</h4>
                  <p>Sourcing Suite</p>
                </div>
              </div>
            </div>
            <div className="login-powered">
              Powered by Hypernix
              <br />
              Version 1.0.0 · © 2026
            </div>
          </div>
        </aside>

        <section className="login-pane-form">{children}</section>
      </div>
    </div>
  );
}

export const AuthUserIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2" />
    <circle cx="12" cy="7" r="4" />
  </svg>
);

export const AuthLockIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
    <path d="M7 11V7a5 5 0 0 1 10 0v4" />
  </svg>
);

export const AuthEyeIcon = ({ off }: { off?: boolean }) =>
  off ? (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94" />
      <path d="M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19" />
      <path d="M14.12 14.12a3 3 0 1 1-4.24-4.24" />
      <line x1="1" y1="1" x2="23" y2="23" />
    </svg>
  ) : (
    <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
      <path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" />
      <circle cx="12" cy="12" r="3" />
    </svg>
  );

export const AuthMailIcon = () => (
  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">
    <path d="M4 4h16c1.1 0 2 .9 2 2v12c0 1.1-.9 2-2 2H4c-1.1 0-2-.9-2-2V6c0-1.1.9-2 2-2z" />
    <polyline points="22,6 12,13 2,6" />
  </svg>
);
