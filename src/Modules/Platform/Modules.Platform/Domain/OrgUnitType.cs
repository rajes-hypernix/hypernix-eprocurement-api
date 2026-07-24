namespace FSH.Modules.Platform.Domain;

public enum OrgUnitType
{
    Department = 0,
    Location = 1,
    CostCentre = 2,

    /// <summary>Segments dimension (Phase 5) — an org-dimension classification tag, distinct from the "Locations" administration area's address-bearing shipping records.</summary>
    Category = 3,

    /// <summary>Segments dimension (Phase 5).</summary>
    Project = 4,
}
