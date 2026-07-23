using FSH.Modules.Platform.Domain;

namespace FSH.Modules.Platform.Data;

/// <summary>Seeds Phase 1 configuration masters (settings, FX, tax, payment terms, numbering, incoterms).</summary>
public static class ConfigurationSeedData
{
    public static void Seed(PlatformDbContext db)
    {
        ArgumentNullException.ThrowIfNull(db);
        SeedSettings(db);
        SeedCurrencies(db);
        SeedExchangeRates(db);
        SeedTaxCodes(db);
        SeedPaymentTerms(db);
        SeedIncoterms(db);
        SeedNumberingSchemes(db);
    }

    private static void SeedSettings(PlatformDbContext db)
    {
        if (db.Settings.Any()) return;
        db.Settings.AddRange(
            Setting.Create(
                SettingKeys.TimeZone,
                "Asia/Kuala_Lumpur",
                "TimeZone",
                "Platform timezone",
                "IANA timezone used for document dates and numbering year buckets.",
                id: Guid.Parse("11111111-1111-1111-1111-111111111001")),
            Setting.Create(
                SettingKeys.BaseCurrency,
                "MYR",
                "Currency",
                "Base currency",
                "Ledger / FX base currency. Exchange rates are expressed as units of this currency per 1 foreign unit.",
                id: Guid.Parse("11111111-1111-1111-1111-111111111002")));
    }

    private static void SeedCurrencies(PlatformDbContext db)
    {
        if (db.Currencies.Any()) return;
        (string Code, string Name, string Symbol, int Decimals, bool Active)[] rows =
        [
            ("MYR", "Malaysian Ringgit", "RM", 2, true),
            ("USD", "US Dollar", "$", 2, true),
            ("SGD", "Singapore Dollar", "S$", 2, true),
            ("EUR", "Euro", "€", 2, true),
            ("GBP", "British Pound", "£", 2, true),
            ("JPY", "Japanese Yen", "¥", 0, false),
            ("CNY", "Chinese Yuan", "¥", 2, false),
            ("AUD", "Australian Dollar", "A$", 2, false),
            ("AED", "UAE Dirham", "د.إ", 2, false),
        ];
        foreach (var r in rows)
            db.Currencies.Add(Currency.Create(r.Code, r.Name, r.Symbol, r.Decimals, r.Active, createdBy: "system"));
    }

    private static void SeedExchangeRates(PlatformDbContext db)
    {
        if (db.ExchangeRates.Any()) return;
        var asOf = new DateOnly(2026, 7, 1);
        (string Code, decimal Rate)[] rows =
        [
            ("USD", 4.70m),
            ("SGD", 3.48m),
            ("EUR", 5.10m),
            ("GBP", 5.95m),
        ];
        foreach (var r in rows)
            db.ExchangeRates.Add(ExchangeRate.Create(r.Code, r.Rate, asOf, "system"));
    }

    private static void SeedTaxCodes(PlatformDbContext db)
    {
        if (db.TaxCodes.Any()) return;
        db.TaxCodes.Add(TaxCode.Create("SST", "Sales & Service Tax", 8.0m, createdBy: "system"));
        db.TaxCodes.Add(TaxCode.Create("ZR", "Zero-rated", 0.0m, createdBy: "system"));
    }

    private static void SeedPaymentTerms(PlatformDbContext db)
    {
        if (db.PaymentTerms.Any()) return;
        db.PaymentTerms.Add(PaymentTerm.Create("NET30", "Net 30 days", PaymentTermKind.Net, dueDays: 30, createdBy: "system"));
        db.PaymentTerms.Add(PaymentTerm.Create("NET60", "Net 60 days", PaymentTermKind.Net, dueDays: 60, createdBy: "system"));
        db.PaymentTerms.Add(PaymentTerm.Create(
            "2/10NET30",
            "2/10 Net 30",
            PaymentTermKind.Net,
            dueDays: 30,
            discountPct: 2m,
            discountDays: 10,
            createdBy: "system"));
        db.PaymentTerms.Add(PaymentTerm.Create(
            "EOM",
            "End of next month",
            PaymentTermKind.DateDriven,
            dayOfMonth: 31,
            monthsAhead: 1,
            minimumDaysBeforeDue: 5,
            createdBy: "system"));
        db.PaymentTerms.Add(PaymentTerm.Create(
            "MILESTONE-50-50",
            "50% advance / 50% in 30 days",
            PaymentTermKind.Schedule,
            rows:
            [
                new PaymentScheduleRowDraft(1, 50m, ScheduleBasis.Advance),
                new PaymentScheduleRowDraft(2, 50m, ScheduleBasis.DaysFromDoc, Days: 30),
            ],
            createdBy: "system"));
    }

    private static void SeedIncoterms(PlatformDbContext db)
    {
        if (db.Incoterms.Any()) return;
        (string Code, string Name)[] rows =
        [
            ("EXW", "Ex Works"),
            ("FCA", "Free Carrier"),
            ("FAS", "Free Alongside Ship"),
            ("FOB", "Free On Board"),
            ("CFR", "Cost and Freight"),
            ("CIF", "Cost, Insurance and Freight"),
            ("CPT", "Carriage Paid To"),
            ("CIP", "Carriage and Insurance Paid To"),
            ("DAP", "Delivered at Place"),
            ("DPU", "Delivered at Place Unloaded"),
            ("DDP", "Delivered Duty Paid"),
        ];
        foreach (var r in rows)
            db.Incoterms.Add(Incoterm.Create(r.Code, r.Name, createdBy: "system"));
    }

    private static void SeedNumberingSchemes(PlatformDbContext db)
    {
        if (db.NumberingSchemes.Any()) return;
        foreach (var recordType in DocumentNumberRecordTypes.All)
        {
            db.NumberingSchemes.Add(NumberingScheme.Create(
                recordType,
                prefix: recordType,
                yearSegment: true,
                digits: 4,
                createdBy: "system"));
        }
    }
}
