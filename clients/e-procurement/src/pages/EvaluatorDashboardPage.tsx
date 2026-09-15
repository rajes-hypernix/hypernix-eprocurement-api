import { useNavigate } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { listBidOpenings } from "@/api/sourcing";
import { getUnreadCount } from "@/api/notifications";
import { Icon } from "@/components/Icon";
import { Spinner } from "@/components/ui";
import { FshPermissions } from "@/lib/fsh-permissions";
import { rfqAssignedToUser, rfqIsReadyToOpen } from "@/lib/evaluation-queue";
import { evaluatorKind } from "@/lib/workspace";
import { useAuth } from "@/auth/use-auth";

/** Evaluator home — assigned bid openings, not buyer P2P KPIs. */
export function EvaluatorDashboardPage() {
  const navigate = useNavigate();
  const { user, rolePreview } = useAuth();
  const granted = user?.permissions ?? [];
  const canOpenings = granted.includes(FshPermissions.evaluation.viewOpening);
  const canNotif = granted.includes(FshPermissions.notifications.view);
  const kind = evaluatorKind({
    roles: user?.roles ?? [],
    permissions: user?.permissions,
    previewRoleName: rolePreview?.roleName,
  });

  const rfqs = useQuery({
    queryKey: ["bid-openings"],
    queryFn: listBidOpenings,
    enabled: canOpenings,
  });
  const unread = useQuery({
    queryKey: ["notifications", "unread-count"],
    queryFn: getUnreadCount,
    enabled: canNotif,
  });

  const queue = (rfqs.data ?? []).filter(rfqIsReadyToOpen);
  const mine = queue.filter((r) => rfqAssignedToUser(r, user?.id));
  const loading = (rfqs.isPending && canOpenings) || (unread.isPending && canNotif);

  const subtitle = kind.tech && !kind.commercial
    ? "Closed RFQs ready for technical opening and scoring."
    : kind.commercial && !kind.tech
      ? "Closed RFQs ready for commercial unseal after technical is finalized."
      : "Closed RFQs ready to open and evaluate.";

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Dashboard</h1>
          <p>{subtitle}</p>
        </div>
      </div>

      {loading ? <Spinner label="Loading dashboard…" /> : null}

      {!loading ? (
        <div className="grid g3" style={{ marginBottom: 16 }}>
          <button
            type="button"
            className="card stat tone-amber"
            style={{ textAlign: "left", cursor: "pointer", width: "100%", fontFamily: "inherit" }}
            onClick={() => void navigate("/openings")}
          >
            <div className="lbl">Ready to evaluate</div>
            <div className="num">{queue.length}</div>
            <div className="sub">
              {mine.length} assigned to you · past close, in evaluation, or awarded
            </div>
          </button>
          {canNotif ? (
            <button
              type="button"
              className="card stat tone-teal"
              style={{ textAlign: "left", cursor: "pointer", width: "100%", fontFamily: "inherit" }}
              onClick={() => void navigate("/notifications")}
            >
              <div className="lbl">Unread notifications</div>
              <div className="num">{unread.data ?? 0}</div>
              <div className="sub">Inbox</div>
            </button>
          ) : null}
        </div>
      ) : null}

      <div className="card">
        <div className="chead">
          <h3>Your queue</h3>
        </div>
        <div className="cbody">
          {queue.length === 0 ? (
            <p className="muted" style={{ margin: 0 }}>
              No RFQs are ready to open yet. Bid openings appear after the close time, or after the buyer closes
              the event early.
            </p>
          ) : (
            <ul style={{ margin: 0, paddingLeft: 18 }}>
              {queue.slice(0, 8).map((r) => (
                <li key={r.id} style={{ marginBottom: 6 }}>
                  <button
                    type="button"
                    className="btn btn-ghost btn-sm"
                    style={{ padding: 0 }}
                    onClick={() => void navigate(`/openings/${r.id}`)}
                  >
                    {r.code} · {r.title}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>

      <div className="card" style={{ marginTop: 14 }}>
        <div className="chead">
          <h3>Shortcuts</h3>
        </div>
        <div className="cbody" style={{ display: "flex", flexWrap: "wrap", gap: 10 }}>
          <button type="button" className="btn btn-out btn-sm" onClick={() => void navigate("/openings")}>
            <Icon name="lock" size={14} /> Bid openings
          </button>
          {canNotif ? (
            <button type="button" className="btn btn-out btn-sm" onClick={() => void navigate("/notifications")}>
              <Icon name="bell" size={14} /> Notifications
            </button>
          ) : null}
        </div>
      </div>
    </>
  );
}
