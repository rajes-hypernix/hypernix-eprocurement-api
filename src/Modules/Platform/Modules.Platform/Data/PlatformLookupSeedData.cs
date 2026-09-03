using FSH.Modules.Platform.Contracts;
using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Data;

/// <summary>Idempotent Malaysia-focused geo, banks, and RFQ reason-code lists.</summary>
public static class PlatformLookupSeedData
{
    /// <summary>Full MY state / federal territory set (POC CustomListSeed parity).</summary>
    private static readonly (string Code, string Name, string[] Cities)[] MalaysiaStates =
    [
        ("JHR", "Johor", ["Johor Bahru", "Pasir Gudang", "Batu Pahat", "Skudai"]),
        ("KDH", "Kedah", ["Alor Setar", "Sungai Petani"]),
        ("KTN", "Kelantan", ["Kota Bharu"]),
        ("MLK", "Melaka", ["Melaka City"]),
        ("NSN", "Negeri Sembilan", ["Seremban"]),
        ("PHG", "Pahang", ["Kuantan"]),
        ("PNG", "Pulau Pinang", ["George Town", "Butterworth", "Bayan Lepas"]),
        ("PRK", "Perak", ["Ipoh"]),
        ("PLS", "Perlis", ["Kangar"]),
        ("SGR", "Selangor", ["Shah Alam", "Petaling Jaya", "Subang Jaya", "Klang"]),
        ("TRG", "Terengganu", ["Kuala Terengganu"]),
        ("SBH", "Sabah", ["Kota Kinabalu", "Sandakan", "Tawau"]),
        ("SWK", "Sarawak", ["Kuching", "Miri", "Bintulu", "Sibu"]),
        ("KUL", "Wilayah Persekutuan Kuala Lumpur", ["Kuala Lumpur"]),
        ("LBN", "Wilayah Persekutuan Labuan", ["Labuan"]),
        ("PJY", "Wilayah Persekutuan Putrajaya", ["Putrajaya"]),
    ];

    /// <summary>POC COUNTRY custom-list parity — states/cities stay Malaysia-only until maintained in Setup.</summary>
    private static readonly (string Code, string Name)[] CatalogCountries =
    [
        ("MY", "Malaysia"),
        ("SG", "Singapore"),
        ("BN", "Brunei"),
        ("ID", "Indonesia"),
        ("TH", "Thailand"),
        ("AE", "United Arab Emirates"),
        ("GB", "United Kingdom"),
        ("US", "United States"),
        ("CN", "China"),
        ("JP", "Japan"),
        ("AU", "Australia"),
        ("IN", "India"),
    ];

    public static void Seed(PlatformDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        EnsureCatalogCountries(db);
        EnsureMalaysiaGeo(db);

        if (!db.Banks.Any())
        {
            db.Banks.AddRange(
                Bank.Create("Maybank", "MY", "MBBEMYKL"),
                Bank.Create("CIMB Bank", "MY", "CIBBMYKL"),
                Bank.Create("Public Bank", "MY", "PBBEMYKL"),
                Bank.Create("RHB Bank", "MY", "RHBBMYKL"),
                Bank.Create("Hong Leong Bank", "MY", "HLBBMYKL"));
        }

        if (!db.CustomLists.Any())
        {
            SeedList(db, CustomListKeys.RfqRescind, "RFQ invitation rescind reasons",
            [
                ("DUPLICATE", "Duplicate invitation", 1),
                ("WRONG_VENDOR", "Wrong vendor invited", 2),
                ("CANCELLED_NEED", "Requirement cancelled", 3),
                ("OTHER", "Other", 99),
            ]);

            SeedList(db, CustomListKeys.RfqExtend, "RFQ close-date extension reasons",
            [
                ("MORE_BIDS", "Need more bids", 1),
                ("CLARIFICATION", "Pending clarifications", 2),
                ("HOLIDAY", "Public holiday / downtime", 3),
                ("OTHER", "Other", 99),
            ]);

            SeedList(db, CustomListKeys.BidDecline, "Vendor bid decline reasons",
            [
                ("CAPACITY", "No capacity", 1),
                ("SCOPE", "Out of scope", 2),
                ("COMMERCIAL", "Commercial terms not acceptable", 3),
                ("OTHER", "Other", 99),
            ]);
        }

        EnsureOrgUnits(db);

        if (!db.FormTemplates.Any())
        {
            var swec = FormTemplate.Create(FormTemplateKeys.OnboardingSwec, "SWEC vendor onboarding questionnaire");
            swec.AddQuestion(1, "Primary SWEC discipline experience", "short_text", required: true);
            swec.AddQuestion(2, "Years of relevant experience", "number", required: true);
            swec.AddQuestion(3, "HSE policy acknowledgement", "yesno", required: true);
            db.FormTemplates.Add(swec);

            var nonSwec = FormTemplate.Create(FormTemplateKeys.OnboardingNonSwec, "Non-SWEC vendor onboarding questionnaire");
            nonSwec.AddQuestion(1, "Company overview", "long_text", required: true);
            nonSwec.AddQuestion(2, "Key products / services", "long_text", required: true);
            nonSwec.AddQuestion(3, "Willing to undergo financial assessment?", "yesno", required: true);
            db.FormTemplates.Add(nonSwec);
        }
    }

    private static void EnsureCatalogCountries(PlatformDbContext db)
    {
        var existing = db.Countries
            .Where(c => !c.IsDeleted)
            .Select(c => c.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, name) in CatalogCountries)
        {
            if (existing.Contains(code))
                continue;

            db.Countries.Add(Country.Create(code, name));
            existing.Add(code);
        }
    }

    /// <summary>
    /// Creates Malaysia + full state/city set on empty geo, or inserts any missing MY states
    /// on databases that were seeded with the earlier partial list.
    /// </summary>
    private static void EnsureMalaysiaGeo(PlatformDbContext db)
    {
        var my = db.Countries.FirstOrDefault(c => c.Code == "MY" && !c.IsDeleted);
        if (my is null)
        {
            my = Country.Create("MY", "Malaysia");
            db.Countries.Add(my);
        }

        var existingCodes = db.States
            .Where(s => s.CountryId == my.Id && !s.IsDeleted)
            .Select(s => s.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (code, name, cities) in MalaysiaStates)
        {
            if (existingCodes.Contains(code))
                continue;

            var state = State.Create(my.Id, code, name);
            db.States.Add(state);
            foreach (var city in cities)
                db.Cities.Add(City.Create(state.Id, city));
        }
    }

    /// <summary>
    /// Seeds sample org units across the Classification dimensions used on the PR form.
    /// Additive: missing types/codes are inserted even when some units already exist.
    /// </summary>
    private static void EnsureOrgUnits(PlatformDbContext db)
    {
        var existing = db.OrgUnits
            .Select(u => u.Code)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        void Add(string code, string name, OrgUnitType type)
        {
            if (existing.Contains(code))
                return;
            db.OrgUnits.Add(OrgUnit.Create(code, name, type));
            existing.Add(code);
        }

        Add("OPS", "Operations", OrgUnitType.Department);
        Add("PROC", "Procurement", OrgUnitType.Department);
        Add("FIN", "Finance", OrgUnitType.Department);

        Add("HQ-KL", "Headquarters — Kuala Lumpur", OrgUnitType.Location);
        Add("PLANT-JHR", "Johor Plant", OrgUnitType.Location);
        Add("PLANT-BTU", "Bintulu Plant", OrgUnitType.Location);

        Add("CAT-PIPING", "Piping", OrgUnitType.Category);
        Add("CAT-ELEC", "Electrical", OrgUnitType.Category);
        Add("CAT-MECH", "Mechanical", OrgUnitType.Category);

        Add("PRJ-TA2026", "TA-2026", OrgUnitType.Project);
        Add("PRJ-OPS", "Operations Capex", OrgUnitType.Project);

        Add("CC-1000", "Cost Centre 1000", OrgUnitType.CostCentre);
        Add("CC-2000", "Cost Centre 2000", OrgUnitType.CostCentre);
    }

    private static void SeedList(
        PlatformDbContext db,
        string key,
        string name,
        (string Code, string Label, int Sort)[] items)
    {
        var list = CustomList.Create(key, name);
        foreach (var (code, label, sort) in items)
            list.UpsertItem(code, label, sort, isActive: true);
        db.CustomLists.Add(list);
    }
}
