import { useNavigate, useParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import {
  getMyStatement,
  getStatement,
  listStatements,
  type StatementDetailDto,
} from "@/api/procurement";
import { useAuth } from "@/auth/use-auth";
import { Icon } from "@/components/Icon";
import { EmptyState, Spinner } from "@/components/ui";
import { fmt } from "@/lib/format";

/** Route switch for /statements/* and /statement/* */
export function StatementsPage() {
  const navigate = useNavigate();
  const { isVendor } = useAuth();
  const { "*": rest } = useParams();
  const route = (rest ?? "").replace(/^\/+|\/+$/g, "");

  if (isVendor) return <VendorStatement />;

  if (route) {
    return (
      <StatementDetail
        vendorId={route}
        onBack={() => void navigate("/statements")}
      />
    );
  }

  return (
    <StatementList onOpen={(id) => void navigate(`/statements/${id}`)} />
  );
}

function StatementList({ onOpen }: { onOpen: (id: string) => void }) {
  const { data: rows = [], isPending } = useQuery({
    queryKey: ["statements"],
    queryFn: listStatements,
  });

  const totBal = rows.reduce((a, r) => a + (r.balance ?? 0), 0);
  const totGrni = rows.reduce((a, r) => a + (r.grni ?? 0), 0);
  const withBal = rows.filter((r) => (r.balance ?? 0) > 0).length;

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Statements of Account</h1>
          <p>
            Per-vendor ledger across POs, goods receipts and invoices — with open balance and GRNI
            accruals. Paid is 0 until Payments module exists.
          </p>
        </div>
      </div>

      {isPending ? (
        <Spinner label="Loading statements…" />
      ) : (
        <>
          <div className="grid g3" style={{ marginBottom: 16 }}>
            <div className="card stat tone-teal">
              <div className="lbl">Total payable</div>
              <div className="num" style={{ fontSize: 20 }}>
                RM {fmt(totBal)}
              </div>
              <div className="sub">open balance</div>
            </div>
            <div className="card stat tone-sage">
              <div className="lbl">GRNI accrual</div>
              <div className="num" style={{ fontSize: 20 }}>
                RM {fmt(totGrni)}
              </div>
              <div className="sub">received, not invoiced</div>
            </div>
            <div className="card stat tone-clay">
              <div className="lbl">Vendors with balance</div>
              <div className="num">{withBal}</div>
              <div className="sub">outstanding</div>
            </div>
          </div>

          <div className="card">
            <table>
              <thead>
                <tr>
                  <th>Vendor</th>
                  <th className="amt">Invoiced</th>
                  <th className="amt">Paid</th>
                  <th className="amt">Open balance</th>
                  <th className="amt">GRNI</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {rows.map((r) => (
                  <tr key={r.vendorId} className="rowlink" onClick={() => onOpen(r.vendorId)}>
                    <td style={{ fontWeight: 700 }}>{r.vendorName}</td>
                    <td className="amt">RM {fmt(r.invoiced)}</td>
                    <td className="amt">RM {fmt(r.paid)}</td>
                    <td
                      className="amt"
                      style={{
                        fontWeight: 700,
                        color: (r.balance ?? 0) > 0 ? "var(--teal)" : "inherit",
                      }}
                    >
                      RM {fmt(r.balance)}
                    </td>
                    <td className="amt">{(r.grni ?? 0) > 0 ? `RM ${fmt(r.grni)}` : "—"}</td>
                    <td className="amt">
                      <span className="btn btn-ghost btn-sm">
                        Statement <Icon name="chev" size={13} />
                      </span>
                    </td>
                  </tr>
                ))}
                {rows.length === 0 ? (
                  <tr>
                    <td colSpan={6}>
                      <EmptyState>No vendor statement activity yet.</EmptyState>
                    </td>
                  </tr>
                ) : null}
              </tbody>
            </table>
          </div>
        </>
      )}
    </>
  );
}

function Aging({ s }: { s: StatementDetailDto }) {
  const a = s.aging;
  return (
    <div className="card" style={{ marginBottom: 16 }}>
      <div className="chead">
        <h3>Aged payables</h3>
        <div className="spacer" />
        <span className="hint">unpaid approved invoices</span>
      </div>
      <div className="grid g4" style={{ padding: "14px 16px" }}>
        {(
          [
            ["Current (≤30d)", a?.current],
            ["31–60 days", a?.d30],
            ["61–90 days", a?.d60],
            ["90+ days", a?.d90],
          ] as const
        ).map(([label, v]) => (
          <div className="card stat tone-teal" key={label}>
            <div className="lbl">{label}</div>
            <div className="num" style={{ fontSize: 17 }}>
              RM {fmt(v)}
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

function LedgerCard({ s }: { s: StatementDetailDto }) {
  return (
    <div className="card">
      <div className="chead">
        <h3>Ledger</h3>
        <div className="spacer" />
        <span className="hint">running balance</span>
      </div>
      <table>
        <thead>
          <tr>
            <th>Date</th>
            <th>Reference</th>
            <th>Type</th>
            <th className="amt">Debit (paid)</th>
            <th className="amt">Credit (billed)</th>
            <th className="amt">Balance</th>
          </tr>
        </thead>
        <tbody>
          {(s.ledger ?? []).map((e, i) => (
            <tr key={i}>
              <td>{e.date || "—"}</td>
              <td style={{ fontWeight: 600, color: "var(--teal)" }}>{e.reference}</td>
              <td>
                {e.type}
                {e.memo != null ? <span className="hint"> (RM {fmt(e.memo)})</span> : ""}
              </td>
              <td className="amt">{(e.debit ?? 0) > 0 ? `RM ${fmt(e.debit)}` : ""}</td>
              <td className="amt">{(e.credit ?? 0) > 0 ? `RM ${fmt(e.credit)}` : ""}</td>
              <td className="amt" style={{ fontWeight: 600 }}>
                RM {fmt(e.balance)}
              </td>
            </tr>
          ))}
          {(s.ledger ?? []).length === 0 ? (
            <tr>
              <td colSpan={6}>
                <EmptyState>No ledger activity.</EmptyState>
              </td>
            </tr>
          ) : null}
        </tbody>
      </table>
    </div>
  );
}

function Summary({ s }: { s: StatementDetailDto }) {
  return (
    <div className="grid g4" style={{ marginBottom: 16 }}>
      <div className="card stat tone-teal">
        <div className="lbl">Invoiced</div>
        <div className="num" style={{ fontSize: 18 }}>
          RM {fmt(s.invoiced)}
        </div>
      </div>
      <div className="card stat tone-sage">
        <div className="lbl">Paid</div>
        <div className="num" style={{ fontSize: 18 }}>
          RM {fmt(s.paid)}
        </div>
        <div className="sub">0 until Payments module</div>
      </div>
      <div className="card stat tone-clay">
        <div className="lbl">Open balance</div>
        <div className="num" style={{ fontSize: 18 }}>
          RM {fmt(s.balance)}
        </div>
      </div>
      <div className="card stat tone-amber">
        <div className="lbl">GRNI accrual</div>
        <div className="num" style={{ fontSize: 18 }}>
          RM {fmt(s.grni)}
        </div>
      </div>
    </div>
  );
}

function StatementDetail({ vendorId, onBack }: { vendorId: string; onBack: () => void }) {
  const { data: s, isPending } = useQuery({
    queryKey: ["statement", vendorId],
    queryFn: () => getStatement(vendorId),
  });

  if (isPending || !s) return <Spinner label="Loading statement…" />;

  return (
    <>
      <div className="crumb">
        <a onClick={onBack}>Statements</a> <Icon name="chev" size={13} /> <span>{s.vendorName}</span>
      </div>
      <div className="pagehead">
        <div>
          <h1>{s.vendorName}</h1>
          <p>
            Statement of account · derived from POs, goods receipts and invoices. Paid is 0 until
            Payments module exists.
          </p>
        </div>
        <div className="spacer" />
        <div className="actbar">
          <button type="button" className="btn btn-out" onClick={onBack}>
            <Icon name="back" size={15} /> Back
          </button>
        </div>
      </div>
      <Summary s={s} />
      <Aging s={s} />
      <LedgerCard s={s} />
    </>
  );
}

function VendorStatement() {
  const { data: s, isPending } = useQuery({
    queryKey: ["my-statement"],
    queryFn: getMyStatement,
  });

  if (isPending) return <Spinner label="Loading statement…" />;

  if (!s) {
    return (
      <>
        <div className="pagehead">
          <div>
            <h1>Statement of Account</h1>
            <p>Your running ledger, open balance and goods-received-not-invoiced accrual.</p>
          </div>
        </div>
        <div className="card">
          <div className="cbody">
            <p className="muted">No activity yet.</p>
          </div>
        </div>
      </>
    );
  }

  return (
    <>
      <div className="pagehead">
        <div>
          <h1>Statement of Account</h1>
          <p>
            Your running ledger, open balance and GRNI accrual. Paid is 0 until Payments module
            exists.
          </p>
        </div>
      </div>
      <Summary s={s} />
      <Aging s={s} />
      <LedgerCard s={s} />
    </>
  );
}
