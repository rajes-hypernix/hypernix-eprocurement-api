import { useEffect, useRef, useState, type FormEvent } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  changePassword,
  getMyProfile,
  updateMyProfile,
} from "@/api/identity";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { Notice, Spinner } from "@/components/ui";
import { useErrorDialog } from "@/feedback/ErrorDialogContext";
import { initials } from "@/lib/format";

const PROFILE_KEY = ["identity", "me"] as const;

export function ProfilePage() {
  const { user, applyLocalProfile } = useAuth();
  const { showErrorFrom, showError } = useErrorDialog();
  const queryClient = useQueryClient();

  const profileQuery = useQuery({
    queryKey: PROFILE_KEY,
    queryFn: getMyProfile,
  });

  const profile = profileQuery.data;
  const loading = profileQuery.isLoading;

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [phone, setPhone] = useState("");
  const [profileSaved, setProfileSaved] = useState(false);

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [showCurrent, setShowCurrent] = useState(false);
  const [showNew, setShowNew] = useState(false);
  const [showConfirm, setShowConfirm] = useState(false);
  const [passwordSaved, setPasswordSaved] = useState(false);

  const seededRef = useRef(false);
  useEffect(() => {
    if (seededRef.current) return;
    if (profile) {
      setFirstName(profile.firstName ?? "");
      setLastName(profile.lastName ?? "");
      setPhone(profile.phoneNumber ?? "");
      seededRef.current = true;
    } else if (user && loading) {
      const parts = (user.name ?? "").trim().split(/\s+/);
      setFirstName(parts[0] ?? "");
      setLastName(parts.slice(1).join(" "));
    }
  }, [profile, user, loading]);

  const saveProfile = useMutation({
    mutationFn: () =>
      updateMyProfile({
        firstName: firstName.trim() || null,
        lastName: lastName.trim() || null,
        phoneNumber: phone.trim() || null,
      }),
    onSuccess: () => {
      const display = [firstName.trim(), lastName.trim()].filter(Boolean).join(" ");
      applyLocalProfile({ name: display || user?.email });
      void queryClient.invalidateQueries({ queryKey: PROFILE_KEY });
      setProfileSaved(true);
      window.setTimeout(() => setProfileSaved(false), 3500);
    },
    onError: (e) => showErrorFrom(e, "Could not save profile"),
  });

  const savePassword = useMutation({
    mutationFn: () =>
      changePassword({
        password: currentPassword,
        newPassword,
        confirmNewPassword: confirmPassword,
      }),
    onSuccess: () => {
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
      setPasswordSaved(true);
      window.setTimeout(() => setPasswordSaved(false), 3500);
    },
    onError: (e) => showErrorFrom(e, "Could not change password"),
  });

  const dirty =
    (profile?.firstName ?? "") !== firstName ||
    (profile?.lastName ?? "") !== lastName ||
    (profile?.phoneNumber ?? "") !== phone;

  const onProfileSubmit = (e: FormEvent) => {
    e.preventDefault();
    saveProfile.mutate();
  };

  const onPasswordSubmit = (e: FormEvent) => {
    e.preventDefault();
    if (newPassword.length < 8) {
      showError("Use at least 8 characters for the new password.");
      return;
    }
    if (newPassword !== confirmPassword) {
      showError("New password and confirmation do not match.");
      return;
    }
    savePassword.mutate();
  };

  const onReset = () => {
    if (!profile) return;
    setFirstName(profile.firstName ?? "");
    setLastName(profile.lastName ?? "");
    setPhone(profile.phoneNumber ?? "");
  };

  const displayName =
    [firstName, lastName].filter((s) => s.trim()).join(" ").trim() ||
    profile?.email ||
    user?.email ||
    "User";

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Profile</h1>
          <p>Your account details and sign-in password for this organization.</p>
        </div>
      </div>

      {profileQuery.isError ? (
        <Notice tone="warn" icon="alert">
          Couldn&apos;t load your profile from the server. You can still edit using session details;
          save may fail until the connection recovers.
        </Notice>
      ) : null}

      {profileSaved ? (
        <Notice tone="success" icon="check">
          Profile saved.
        </Notice>
      ) : null}

      {passwordSaved ? (
        <Notice tone="success" icon="check">
          Password updated. Use it the next time you sign in.
        </Notice>
      ) : null}

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="chead">
          <h3>Identity</h3>
          <div className="spacer" />
          <button
            type="button"
            className="btn btn-out btn-sm"
            disabled={saveProfile.isPending || !dirty}
            onClick={onReset}
          >
            Reset
          </button>
          <button
            type="submit"
            form="profile-identity-form"
            className="btn btn-pri btn-sm"
            disabled={saveProfile.isPending || !dirty || loading}
          >
            {saveProfile.isPending ? "Saving…" : "Save changes"}
          </button>
        </div>
        <div className="cbody">
          {loading && !profile ? (
            <Spinner label="Loading profile…" />
          ) : (
            <form id="profile-identity-form" onSubmit={onProfileSubmit}>
              <div className="profile-identity-row">
                <div className="profile-avatar" aria-hidden="true">
                  {profile?.imageUrl ? (
                    <img src={profile.imageUrl} alt="" />
                  ) : (
                    <span>{initials(displayName)}</span>
                  )}
                </div>
                <div className="profile-fields">
                  <div className="profile-field-grid">
                    <div className="field">
                      <label htmlFor="profile-first">First name</label>
                      <input
                        id="profile-first"
                        value={firstName}
                        onChange={(e) => setFirstName(e.target.value)}
                        autoComplete="given-name"
                        disabled={loading}
                      />
                    </div>
                    <div className="field">
                      <label htmlFor="profile-last">Last name</label>
                      <input
                        id="profile-last"
                        value={lastName}
                        onChange={(e) => setLastName(e.target.value)}
                        autoComplete="family-name"
                        disabled={loading}
                      />
                    </div>
                    <div className="field">
                      <label htmlFor="profile-email">Email</label>
                      <input
                        id="profile-email"
                        type="email"
                        value={profile?.email ?? user?.email ?? ""}
                        readOnly
                        disabled
                      />
                      <p className="hint" style={{ marginTop: 6 }}>
                        Contact your organization admin to change your sign-in email.
                      </p>
                    </div>
                    <div className="field">
                      <label htmlFor="profile-phone">Phone</label>
                      <input
                        id="profile-phone"
                        type="tel"
                        value={phone}
                        onChange={(e) => setPhone(e.target.value)}
                        autoComplete="tel"
                        placeholder="+60 12-345 6789"
                        disabled={loading}
                      />
                    </div>
                  </div>
                </div>
              </div>
            </form>
          )}
        </div>
      </div>

      <div className="card">
        <div className="chead">
          <h3>Password</h3>
          <div className="spacer" />
          <button
            type="submit"
            form="profile-password-form"
            className="btn btn-pri btn-sm"
            disabled={
              savePassword.isPending ||
              !currentPassword ||
              !newPassword ||
              !confirmPassword
            }
          >
            <Icon name="lock" size={14} />
            {savePassword.isPending ? "Updating…" : "Update password"}
          </button>
        </div>
        <div className="cbody">
          <p className="hint" style={{ marginTop: 0, marginBottom: 14 }}>
            Choose a strong password you don&apos;t reuse elsewhere. Minimum 8 characters.
          </p>
          <form id="profile-password-form" onSubmit={onPasswordSubmit}>
            <div className="profile-field-grid">
              <PasswordField
                id="profile-current-pw"
                label="Current password"
                value={currentPassword}
                onChange={setCurrentPassword}
                show={showCurrent}
                onToggleShow={() => setShowCurrent((v) => !v)}
                autoComplete="current-password"
              />
              <div className="field" />
              <PasswordField
                id="profile-new-pw"
                label="New password"
                value={newPassword}
                onChange={setNewPassword}
                show={showNew}
                onToggleShow={() => setShowNew((v) => !v)}
                autoComplete="new-password"
                minLength={8}
              />
              <PasswordField
                id="profile-confirm-pw"
                label="Confirm new password"
                value={confirmPassword}
                onChange={setConfirmPassword}
                show={showConfirm}
                onToggleShow={() => setShowConfirm((v) => !v)}
                autoComplete="new-password"
                minLength={8}
              />
            </div>
          </form>
        </div>
      </div>
    </>
  );
}

function PasswordField({
  id,
  label,
  value,
  onChange,
  show,
  onToggleShow,
  autoComplete,
  minLength,
}: {
  id: string;
  label: string;
  value: string;
  onChange: (v: string) => void;
  show: boolean;
  onToggleShow: () => void;
  autoComplete: string;
  minLength?: number;
}) {
  return (
    <div className="field">
      <label htmlFor={id}>{label}</label>
      <div className="profile-pw-wrap">
        <input
          id={id}
          type={show ? "text" : "password"}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          autoComplete={autoComplete}
          required
          minLength={minLength}
        />
        <button
          type="button"
          className="profile-pw-eye"
          aria-label={show ? "Hide password" : "Show password"}
          onClick={onToggleShow}
        >
          <Icon name={show ? "eye-off" : "eye"} size={16} />
        </button>
      </div>
    </div>
  );
}
