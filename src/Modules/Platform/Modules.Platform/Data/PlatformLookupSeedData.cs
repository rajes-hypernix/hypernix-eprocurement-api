using FSH.Modules.Platform.Contracts;
using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Data;

/// <summary>Idempotent Malaysia-focused geo, banks, and RFQ reason-code lists.</summary>
public static class PlatformLookupSeedData
{
    public static void Seed(PlatformDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);

        if (!db.Countries.Any())
        {
            var my = Country.Create("MY", "Malaysia");
            db.Countries.Add(my);

            var states = new (string Code, string Name, string[] Cities)[]
            {
                ("SGR", "Selangor", ["Shah Alam", "Petaling Jaya", "Subang Jaya"]),
                ("KUL", "Wilayah Persekutuan Kuala Lumpur", ["Kuala Lumpur"]),
                ("JHR", "Johor", ["Johor Bahru", "Skudai"]),
                ("PNG", "Pulau Pinang", ["George Town", "Bayan Lepas"]),
                ("SBH", "Sabah", ["Kota Kinabalu"]),
                ("SWK", "Sarawak", ["Kuching"]),
            };

            foreach (var (code, name, cities) in states)
            {
                var state = State.Create(my.Id, code, name);
                db.States.Add(state);
                foreach (var city in cities)
                    db.Cities.Add(City.Create(state.Id, city));
            }
        }

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

        if (!db.OrgUnits.Any())
        {
            db.OrgUnits.AddRange(
                OrgUnit.Create("OPS", "Operations", OrgUnitType.Department),
                OrgUnit.Create("PROC", "Procurement", OrgUnitType.Department),
                OrgUnit.Create("FIN", "Finance", OrgUnitType.Department),
                OrgUnit.Create("HQ-KL", "Headquarters — Kuala Lumpur", OrgUnitType.Location),
                OrgUnit.Create("PLANT-JHR", "Johor Plant", OrgUnitType.Location),
                OrgUnit.Create("CC-1000", "Cost Centre 1000", OrgUnitType.CostCentre),
                OrgUnit.Create("CC-2000", "Cost Centre 2000", OrgUnitType.CostCentre));
        }

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
