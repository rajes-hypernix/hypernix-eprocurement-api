namespace eProcure.Api.Auth;

/// <summary>A seeded internal user available for dev JWT login (SEED-DATA.md §2).</summary>
public sealed record DevUser(string Id, string Name, string Email, string[] Roles);

/// <summary>
/// Static list of the seeded internal users (SEED-DATA.md §2). Used by the dev
/// login endpoint to issue JWTs with role claims before the User table exists
/// (Slice 1). Internal users are never vendors.
/// </summary>
public sealed class DevUserStore
{
    public IReadOnlyList<DevUser> Users { get; } =
    [
        new("u_faridah", "Faridah Yusof",     "faridah@hypernix.test", ["Buyer"]),
        new("u_lim",     "Lim Chee Kong",     "lim@hypernix.test",     ["Buyer", "Approver"]),
        new("u_hafiz",   "Ir. Hafiz Rahman",  "hafiz@hypernix.test",   ["TechEvaluator"]),
        new("u_nur",     "Nurul Aina",        "nur@hypernix.test",     ["TechEvaluator"]),
        new("u_raj",     "Rajesh Kumar",      "raj@hypernix.test",     ["TechEvaluator"]),
        new("u_tan",     "Tan Mei Ling",      "tan@hypernix.test",     ["CommEvaluator"]),
        new("u_admin",   "System Admin",      "admin@hypernix.test",   ["Admin"]),
    ];

    public DevUser? Find(string idOrEmail) =>
        Users.FirstOrDefault(u =>
            string.Equals(u.Id, idOrEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(u.Email, idOrEmail, StringComparison.OrdinalIgnoreCase));
}
