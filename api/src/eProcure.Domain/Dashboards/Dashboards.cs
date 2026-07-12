namespace eProcure.Domain.Dashboards;

/// <summary>
/// The eight portlet types (D4, OD-D4-2 ruled MyInvitations in as the eighth: the vendor's
/// primary work surface is computed and real — flattening it into a SavedViewList would lose
/// the bid actions). Each type's ConfigJson schema is documented on
/// <c>Application/Dashboards/PortletConfigSchemas</c> — the framework's ONE sanctioned JSON.
/// </summary>
public enum PortletType { KpiMeter, KpiScorecard, Reminders, SavedViewList, Shortcuts, RecentRecords, Chart, MyInvitations }

/// <summary>
/// A dashboard: either a ROLE DEFAULT (OwnerRole set, IsRoleDefault, Admin-managed via A64)
/// or a user's personalized copy (OwnerUserId set — copy-on-write from the resolved
/// role-default union). Exactly one owner axis, enforced by a DB check constraint.
/// Grain: one row per dashboard.
/// </summary>
public class Dashboard
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Code { get; set; } = default!;        // DASH-SYS-* seeds; DASH-2026-#### for personal copies
    public string Name { get; set; } = default!;
    public string? OwnerRole { get; set; }              // a Roles.* value for role defaults
    public string? OwnerUserId { get; set; }            // principal id for personalized copies
    public bool IsRoleDefault { get; set; }
    public List<PortletInstance> Portlets { get; set; } = [];
    public DateTime CreatedUtc { get; set; }
    public DateTime UpdatedUtc { get; set; }
}

/// <summary>One portlet on one dashboard. Row/Col/Width are the arrange grid (2-column:
/// Width 1 = half, 2 = full; arrow-based reorder writes them). Grain: one row per placement.</summary>
public class PortletInstance
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid DashboardId { get; set; }
    public PortletType PortletType { get; set; }
    public string Title { get; set; } = default!;
    public short Col { get; set; }
    public short Row { get; set; }
    public short Width { get; set; } = 1;
    public Guid? SavedViewId { get; set; }              // SavedViewList / view-backed KpiMeter
    /// <summary>The framework's ONE sanctioned JSON — schema per PortletType, validated on save.</summary>
    public string ConfigJson { get; set; } = "{}";
}
