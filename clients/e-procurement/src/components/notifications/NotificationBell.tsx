import { useEffect, useRef, useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  getUnreadCount,
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type NotificationDto,
} from "@/api/notifications";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { FshPermissions } from "@/lib/fsh-permissions";
import { formatRelativeShort, toClientNotificationLink } from "@/lib/notification-links";

/**
 * Topbar bell with unread badge + recent preview. Polls unread count;
 * SignalR live updates are deferred (admin/dashboard have realtime — e-proc uses poll).
 */
export function NotificationBell() {
  const { isAuthenticated, user, permissionsHydrated } = useAuth();
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  const canView =
    permissionsHydrated &&
    (user?.permissions ?? []).includes(FshPermissions.notifications.view);

  const unread = useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: getUnreadCount,
    enabled: isAuthenticated && canView,
    staleTime: 30_000,
    refetchInterval: 60_000,
  });

  const recent = useQuery({
    queryKey: ["notifications", "recent"],
    queryFn: () => listNotifications({ pageSize: 8 }),
    enabled: isAuthenticated && canView && open,
    staleTime: 15_000,
  });

  useEffect(() => {
    if (!open) return;
    const onKey = (e: KeyboardEvent) => {
      if (e.key === "Escape") setOpen(false);
    };
    const onPointer = (e: MouseEvent) => {
      if (rootRef.current && !rootRef.current.contains(e.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener("keydown", onKey);
    document.addEventListener("mousedown", onPointer);
    return () => {
      document.removeEventListener("keydown", onKey);
      document.removeEventListener("mousedown", onPointer);
    };
  }, [open]);

  const markOne = useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["notifications"] }),
  });

  const markAll = useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["notifications"] }),
  });

  if (!isAuthenticated || !canView) return null;

  const count = unread.data ?? 0;
  const items = recent.data ?? [];

  return (
    <div className="notif-bell" ref={rootRef}>
      <button
        type="button"
        className="notif-bell-btn"
        onClick={() => setOpen((v) => !v)}
        aria-label={count > 0 ? `${count} unread notifications` : "Notifications"}
        aria-haspopup="true"
        aria-expanded={open}
      >
        <Icon name="bell" size={16} />
        {count > 0 ? (
          <span className="notif-badge" aria-hidden>
            {count > 99 ? "99+" : count}
          </span>
        ) : null}
      </button>

      {open ? (
        <div className="notif-pop" aria-label="Notifications">
          <div className="notif-pop-head">
            <strong>Notifications</strong>
            {count > 0 ? (
              <button
                type="button"
                className="lnk"
                disabled={markAll.isPending}
                onClick={() => markAll.mutate()}
              >
                Mark all read
              </button>
            ) : null}
          </div>

          <div className="notif-pop-body">
            {recent.isLoading ? (
              <p className="hint" style={{ padding: "18px 14px", textAlign: "center" }}>
                Loading…
              </p>
            ) : null}
            {!recent.isLoading && items.length === 0 ? (
              <p className="hint" style={{ padding: "22px 14px", textAlign: "center" }}>
                You&apos;re all caught up.
              </p>
            ) : null}
            <ul className="notif-list">
              {items.map((n) => (
                <NotifRow
                  key={n.id}
                  notif={n}
                  onMarkRead={() => markOne.mutate(n.id)}
                  onNavigate={() => setOpen(false)}
                />
              ))}
            </ul>
          </div>

          <div className="notif-pop-foot">
            <Link to="/notifications" className="lnk" onClick={() => setOpen(false)}>
              View all
            </Link>
          </div>
        </div>
      ) : null}
    </div>
  );
}

function NotifRow({
  notif,
  onMarkRead,
  onNavigate,
}: {
  notif: NotificationDto;
  onMarkRead: () => void;
  onNavigate: () => void;
}) {
  const unread = !notif.readAtUtc;
  const href = toClientNotificationLink(notif.link);
  const body = (
    <div className="notif-row-text">
      <div className="notif-row-title">
        <span>{notif.title}</span>
        <span className="hint">{formatRelativeShort(notif.createdAtUtc)}</span>
      </div>
      {notif.body ? <p className="notif-row-body">{notif.body}</p> : null}
    </div>
  );

  return (
    <li className={`notif-row${unread ? " unread" : ""}`}>
      <span className={`notif-dot${unread ? " on" : ""}`} aria-hidden />
      {href ? (
        <Link to={href} className="notif-row-link" onClick={onNavigate}>
          {body}
        </Link>
      ) : (
        body
      )}
      {unread ? (
        <button
          type="button"
          className="btn btn-ghost btn-sm"
          aria-label="Mark as read"
          onClick={(e) => {
            e.preventDefault();
            e.stopPropagation();
            onMarkRead();
          }}
        >
          <Icon name="check" size={13} />
        </button>
      ) : null}
    </li>
  );
}
