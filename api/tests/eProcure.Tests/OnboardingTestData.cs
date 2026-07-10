using eProcure.Domain.Onboarding;
using eProcure.Domain.Suppliers;

namespace eProcure.Tests;

/// <summary>
/// Shared fixtures for the vendor-onboarding tests: the prototype's exact financial seed vectors
/// (FIN_C / FIN_D from vendor-onboarding-mockup.html) and small aggregate builders.
/// </summary>
public static class OnboardingTestData
{
    public static readonly DateTime T = new(2026, 7, 1, 9, 0, 0, DateTimeKind.Utc);

    private static FinancialYearFigures Y(int i, decimal rev, decimal np, decimal ebit, decimal ta,
        decimal ca, decimal inv, decimal cl, decimal tl, decimal eq, decimal re, decimal fa) =>
        new(i)
        {
            Revenue = rev, NetProfit = np, Ebit = ebit, TotalAssets = ta, CurrentAssets = ca,
            Inventory = inv, CurrentLiabilities = cl, TotalLiabilities = tl, Equity = eq,
            RetainedEarnings = re, FixedAssets = fa,
        };

    /// <summary>Prototype FIN_C (healthy) — indices 0/1/2 = FY-2/FY-1/current.</summary>
    public static List<FinancialYearFigures> FinC() =>
    [
        Y(0, 18000, 600, 1100, 20000, 9000, 3500, 7500, 13000, 7000, 3000, 8500),
        Y(1, 17000, 400, 800, 20500, 8800, 3800, 8200, 14000, 6500, 2700, 8800),
        Y(2, 16500, 350, 750, 21000, 8600, 4000, 8800, 14800, 6200, 2400, 9000),
    ];

    /// <summary>Prototype FIN_D (distressed).</summary>
    public static List<FinancialYearFigures> FinD() =>
    [
        Y(0, 9000, 200, 450, 11000, 5000, 2200, 5500, 8500, 2500, 900, 5500),
        Y(1, 8200, -150, 100, 10800, 4600, 2400, 6000, 9200, 1600, 500, 5800),
        Y(2, 7500, -400, -200, 10500, 4200, 2500, 6500, 9800, 700, 100, 6000),
    ];

    public static VendorOnboardingInvitation Invitation(VendorType type = VendorType.NonSwec,
        IEnumerable<Guid>? templateIds = null, string rawToken = "raw-token-abc123") =>
        VendorOnboardingInvitation.Create("vieshall@hypernix.net", type, templateIds ?? [], rawToken,
            "u_faridah", "Faridah Yusof", T);

    /// <summary>An invited application (optionally with a FIN_C financial assessment attached).</summary>
    public static VendorOnboardingApplication Application(string code = "VOB-2026-0001",
        VendorType type = VendorType.NonSwec, bool withFinancials = false)
    {
        var app = VendorOnboardingApplication.CreateFromInvitation(code, Invitation(type), T);
        app.Name = "Pacific Valve Sdn Bhd";
        app.RegistrationNo = "1188221-P";
        if (withFinancials)
            app.Financial = new VendorFinancialAssessment(app.Id, FinC(), T);
        return app;
    }

    /// <summary>Drives an application from Invited to UnderReview (the common test pre-state).</summary>
    public static VendorOnboardingApplication UnderReview(bool withFinancials = false)
    {
        var app = Application(withFinancials: withFinancials);
        app.MarkInProgress(T);
        app.Submit(T);
        app.StartReview(T);
        return app;
    }
}
