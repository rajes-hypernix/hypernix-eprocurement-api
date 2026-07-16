namespace FSH.Modules.Suppliers.Services.Onboarding;

public sealed class OnboardingOptions
{
    public string PortalBaseUrl { get; set; } = "http://localhost:5173/";

    /// <summary>Non-prod: route ALL onboarding email here instead of the real recipient.</summary>
    public string? TestRecipientOverride { get; set; }

    public int LinkExpiryDays { get; set; } = 14;
    public string DefaultVendorEmail { get; set; } = "vendor-invites@hypernix.test";
}
