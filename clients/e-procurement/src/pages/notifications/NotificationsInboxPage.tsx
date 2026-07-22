import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  listNotifications,
  markAllNotificationsRead,
  markNotificationRead,
  type NotificationDto,
} from "@/api/notifications";
import { Icon } from "@/components/Icon";
import { EmptyState, Notice, Spinner } from "@/components/ui";
import { ApiRequestError } from "@/lib/api-client";
import { dateTimeMY } from "@/lib/format";
import { toClientNotificationLink } from "@/lib/notification-links";

type Filter = "all" | "unread";

export function NotificationsInboxPage() {
  const queryClient = useQueryClient();
  const [filter, setFilter] = useState<Filter>("unread");

  const query = useQuery({
    queryKey: ["notifications", "inbox", filter],
    queryFn: () => listNotifications({ unreadOnly: filter === "unread", pageSize: 100 }),
    staleTime: 15_000,
  });

  const markOne = useMutation({
    mutationFn: (id: string) => markNotificationRead(id),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["notifications"] }),
  });

  const markAll = useMutation({
    mutationFn: markAllNotificationsRead,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: ["notifications"] }),
  });

  const items = query.data ?? [];
  const err =
    query.error instanceof ApiRequestError
      ? (query.error.problem?.detail ?? query.error.message)
      : query.isError
        ? "Failed to load notifications."
        : null;

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Notifications</h1>
          <p>Events the system has surfaced for you — RFQ releases, awards, onboarding, and more.</p>
        </div>
        <div className="spacer" />
        <button
          type="button"
          className="btn btn-out btn-sm"
          disabled={query.isFetching}
          onClick={() => void query.refetch()}
        >
          <Icon name="clock" size={14} /> Refresh
        </button>
        <button
          type="button"
          className="btn btn-pri btn-sm"
          disabled={markAll.isPending}
          onClick={() => markAll.mutate()}
        >
          <Icon name="check" size={14} /> Mark all read
        </button>
      </div>

      <div className="card" style={{ marginBottom: 14 }}>
        <div className="cbody">
          <div className="filterbar">
            <div className="field" style={{ margin: 0, minWidth: 160 }}>
              <label>Show</label>
              <select value={filter} onChange={(e) => setFilter(e.target.value as Filter)}>
                <option value="unread">Unread</option>
                <option value="all">All</option>
              </select>
            </div>
          </div>
        </div>
      </div>

      {err ? (
        <Notice tone="error" icon="x">
          {err}
        </Notice>
      ) : null}

      {query.isLoading ? <Spinner label="Loading notifications…" /> : null}

      {!query.isLoading && items.length === 0 && !err ? (
        <div className="card">
          <EmptyState>
            {filter === "unread" ? "Nothing unread — you're all caught up." : "No notifications yet."}
          </EmptyState>
        </div>
      ) : null}

      {items.length > 0 ? (
        <div className="card">
          <ul className="notif-inbox">
            {items.map((n) => (
              <InboxRow key={n.id} notif={n} onMarkRead={() => markOne.mutate(n.id)} />
            ))}
          </ul>
        </div>
      ) : null}
    </>
  );
}

function InboxRow({ notif, onMarkRead }: { notif: NotificationDto; onMarkRead: () => void }) {
  const unread = !notif.readAtUtc;
  const href = toClientNotificationLink(notif.link);

  return (
    <li className={`notif-inbox-row${unread ? " unread" : ""}`}>
      <span className={`notif-dot${unread ? " on" : ""}`} aria-hidden />
      <div className="notif-inbox-main">
        <div className="notif-row-title">
          <strong>{notif.title}</strong>
          <span className="badge b-grey">{notif.source}</span>
          <span className="hint">{dateTimeMY(notif.createdAtUtc)}</span>
        </div>
        {notif.body ? <p className="notif-row-body">{notif.body}</p> : null}
        {href ? (
          <Link to={href} className="lnk" style={{ fontSize: 12.5 }}>
            Open <Icon name="chev" size={12} />
          </Link>
        ) : null}
      </div>
      {unread ? (
        <button type="button" className="btn btn-out btn-sm" onClick={onMarkRead}>
          Mark read
        </button>
      ) : null}
    </li>
  );
}
