using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Dashboards;
using eProcure.Domain;
using eProcure.Domain.Dashboards;
using eProcure.Domain.Identity;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Dashboard resolution + personalization (D4 Step 0(d), as ruled). MINE = the caller's
/// personalized copy if one exists, else the DEDUPLICATED UNION of their roles' defaults —
/// reproducing the legacy DashboardService's role-branch merge exactly (u_lim = Buyer cards
/// + the Approver card). Personalize is copy-on-write; Reset deletes the copy; role defaults
/// are Admin-managed (A64) and never mutated by personal edits.
/// </summary>
public sealed class DashboardStoreService(AppDbContext db, IClock clock, ICodeGenerator codes, ICurrentUser user) : IDashboardStore
{
    public async Task<UserDashboardDto> MineAsync(CancellationToken ct = default)
    {
        var mine = await db.Dashboards.AsNoTracking().Include(d => d.Portlets)
            .FirstOrDefaultAsync(d => d.OwnerUserId == user.UserId, ct);
        if (mine is not null) return ToDto(mine, personalized: true);
        var (name, portlets) = await ResolveDefaultUnionAsync(ct);
        return new UserDashboardDto(Guid.Empty, name, false, portlets.Select(ToDto).ToList());
    }

    public async Task<UserDashboardDto> PersonalizeAsync(CancellationToken ct = default)
    {
        var existing = await db.Dashboards.Include(d => d.Portlets)
            .FirstOrDefaultAsync(d => d.OwnerUserId == user.UserId, ct);
        if (existing is not null) return ToDto(existing, personalized: true);   // idempotent

        var (name, portlets) = await ResolveDefaultUnionAsync(ct);
        var copy = new Dashboard
        {
            Code = await codes.NextAsync("DASH", ct),
            Name = name,
            OwnerUserId = user.UserId,
            OwnerRole = null,
            IsRoleDefault = false,
            CreatedUtc = clock.UtcNow,
            UpdatedUtc = clock.UtcNow,
        };
        copy.Portlets.AddRange(portlets.Select(p => new PortletInstance
        {
            DashboardId = copy.Id,
            PortletType = p.PortletType,
            Title = p.Title,
            Col = p.Col, Row = p.Row, Width = p.Width,
            SavedViewId = p.SavedViewId,
            ConfigJson = p.ConfigJson,
        }));
        db.Dashboards.Add(copy);
        await db.SaveChangesAsync(ct);
        return ToDto(copy, personalized: true);
    }

    public async Task<UserDashboardDto> UpdateMineAsync(UpdateDashboardRequest req, CancellationToken ct = default)
    {
        var mine = await db.Dashboards.Include(d => d.Portlets)
            .FirstOrDefaultAsync(d => d.OwnerUserId == user.UserId, ct)
            ?? throw new NotFoundException("No personalized dashboard — personalize first (copy-on-write).");
        await ApplyAsync(mine, req, ct);
        return ToDto(mine, personalized: true);
    }

    public async Task ResetAsync(CancellationToken ct = default)
    {
        var mine = await db.Dashboards.Include(d => d.Portlets)
            .FirstOrDefaultAsync(d => d.OwnerUserId == user.UserId, ct);
        if (mine is null) return;                                   // already on the role default
        db.RemoveRange(mine.Portlets);
        db.Dashboards.Remove(mine);
        await db.SaveChangesAsync(ct);
    }

    public async Task<UserDashboardDto> UpdateRoleDefaultAsync(string role, UpdateDashboardRequest req, CancellationToken ct = default)
    {
        if (!Roles.All.Contains(role))
            throw new DashboardValidationException($"Unknown role '{role}'.");
        var dash = await db.Dashboards.Include(d => d.Portlets)
            .FirstOrDefaultAsync(d => d.OwnerRole == role && d.IsRoleDefault, ct)
            ?? throw new NotFoundException($"No role default for {role}.");
        await ApplyAsync(dash, req, ct);
        return ToDto(dash, personalized: false);
    }

    private async Task ApplyAsync(Dashboard dash, UpdateDashboardRequest req, CancellationToken ct)
    {
        if (req.Portlets.Count == 0)
            throw new DashboardValidationException("A dashboard needs at least one portlet.");
        foreach (var p in req.Portlets)
        {
            if (!Enum.TryParse<PortletType>(p.PortletType, ignoreCase: true, out var type))
                throw new DashboardValidationException($"Unknown portlet type '{p.PortletType}'.");
            PortletConfigs.Validate(type, p.ConfigJson, p.SavedViewId);   // the ONE sanctioned JSON, validated on save
        }
        if (req.Name is not null) dash.Name = req.Name.Trim();
        // Restrict FK: replace children explicitly. The new children carry preset Guid PKs, so
        // they MUST be AddRange()d — children merely discovered on a tracked parent's nav are
        // classified Modified (an UPDATE of a nonexistent row → spurious concurrency conflict).
        db.RemoveRange(dash.Portlets.ToList());
        var replacement = req.Portlets.Select(p => new PortletInstance
        {
            DashboardId = dash.Id,
            PortletType = Enum.Parse<PortletType>(p.PortletType, ignoreCase: true),
            Title = p.Title,
            Col = p.Col, Row = p.Row, Width = p.Width,
            SavedViewId = p.SavedViewId,
            ConfigJson = p.ConfigJson,
        }).ToList();
        db.AddRange(replacement);
        dash.Portlets = replacement;
        dash.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    /// <summary>The union rule: concatenate the caller's roles' defaults in role order,
    /// dedup identical portlets (type + title + view + config), re-rowed sequentially.</summary>
    private async Task<(string Name, List<PortletInstance> Portlets)> ResolveDefaultUnionAsync(CancellationToken ct)
    {
        var roles = Roles.All.Where(user.Roles.Contains).ToList();
        var defaults = await db.Dashboards.AsNoTracking().Include(d => d.Portlets)
            .Where(d => d.IsRoleDefault && d.OwnerRole != null && roles.Contains(d.OwnerRole)).ToListAsync(ct);
        defaults = roles.Select(r => defaults.FirstOrDefault(d => d.OwnerRole == r)).Where(d => d is not null).Select(d => d!).ToList();

        if (defaults.Count == 0)
            return ("Dashboard", []);                               // roleless principal → empty dashboard (welcome state renders client-side)

        // Stack each subsequent role's portlets BELOW the previous role's rows; dedup exact
        // duplicates (identical type + title + view + config) across roles.
        var seen = new HashSet<string>();
        var result = new List<PortletInstance>();
        short rowOffset = 0;
        foreach (var d in defaults)
        {
            short maxRow = -1;
            foreach (var p in d.Portlets.OrderBy(p => p.Row).ThenBy(p => p.Col))
            {
                if (!seen.Add($"{p.PortletType}|{p.Title}|{p.SavedViewId}|{p.ConfigJson}")) continue;
                result.Add(new PortletInstance
                {
                    DashboardId = Guid.Empty,
                    PortletType = p.PortletType,
                    Title = p.Title,
                    Col = p.Col,
                    Row = (short)(rowOffset + p.Row),
                    Width = p.Width,
                    SavedViewId = p.SavedViewId,
                    ConfigJson = p.ConfigJson,
                });
                if (p.Row > maxRow) maxRow = p.Row;
            }
            if (maxRow >= 0) rowOffset = (short)(rowOffset + maxRow + 1);
        }
        return (defaults[0].Name, result);
    }

    private static PortletDto ToDto(PortletInstance p) =>
        new(p.Id, p.PortletType.ToString(), p.Title, p.Col, p.Row, p.Width, p.SavedViewId, p.ConfigJson);

    private static UserDashboardDto ToDto(Dashboard d, bool personalized) =>
        new(d.Id, d.Name, personalized,
            d.Portlets.OrderBy(p => p.Row).ThenBy(p => p.Col).Select(ToDto).ToList());
}
