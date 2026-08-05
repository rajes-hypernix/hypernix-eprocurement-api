using System.IdentityModel.Tokens.Jwt;
using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.Abstractions;
using FSH.Framework.Shared.Constants;
using FSH.Framework.Shared.Multitenancy;
using FSH.Modules.Identity.Domain;
using FSH.Modules.Sourcing.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.Dtos;
using Integration.Tests.Infrastructure;
using Integration.Tests.Infrastructure.Extensions;
using Integration.Tests.Tests.Sessions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;

namespace Integration.Tests.Tests.Eprocure;

/// <summary>
/// Shared HTTP + Identity helpers for Suppliers/Sourcing golden-path integration tests.
/// Root Admin already holds every permission via the seed sync path; Approver and Vendor
/// actors are created explicitly where segregation-of-duties or vendorId claims matter.
/// </summary>
internal static class EprocureFlowHelper
{
    public const string DefaultVendorPassword = "Vendor@Test1!";

    public static async Task AssignRoleAsync(HttpClient adminClient, string userId, string roleName, bool enabled = true)
    {
        using var response = await adminClient.PostAsJsonAsync(
            $"{TestConstants.IdentityBasePath}/users/{userId}/roles",
            new
            {
                userId,
                userRoles = new[] { new { roleName, enabled } }
            });
        response.StatusCode.ShouldBe(HttpStatusCode.OK,
            $"Assign role '{roleName}' failed: {await response.Content.ReadAsStringAsync()}");
    }

    /// <summary>
    /// Creates a loginable user and assigns the seeded Approver role (Award.View + Award.Approve).
    /// </summary>
    public static async Task<SeededUser> CreateApproverAsync(
        FshWebApplicationFactory factory,
        HttpClient adminClient)
    {
        var user = await IdentityUserSeeder.CreateLoginableUserAsync(factory, adminClient, "approver");
        await AssignRoleAsync(adminClient, user.UserId, "Approver");
        return user;
    }

    /// <summary>
    /// Forces a known password on an already-confirmed Identity user (used after onboarding
    /// approve, which registers the vendor with a throwaway temp password + forgot-password email).
    /// Tenant context is set inline — Finbuckle's AsyncLocal is lost across awaited helpers.
    /// </summary>
    public static async Task SetPasswordAsync(
        FshWebApplicationFactory factory,
        string email,
        string newPassword,
        string tenant = TestConstants.RootTenantId)
    {
        using var scope = factory.Services.CreateScope();
        var tenantInfo = await scope.ServiceProvider
            .GetRequiredService<IMultiTenantStore<AppTenantInfo>>()
            .GetAsync(tenant);
        scope.ServiceProvider.GetRequiredService<IMultiTenantContextSetter>()
            .MultiTenantContext = new MultiTenantContext<AppTenantInfo>(tenantInfo!);

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<FshUser>>();
        var user = await userManager.FindByEmailAsync(email);
        user.ShouldNotBeNull($"Identity user '{email}' was not provisioned.");

        var token = await userManager.GeneratePasswordResetTokenAsync(user!);
        var result = await userManager.ResetPasswordAsync(user!, token, newPassword);
        result.Succeeded.ShouldBeTrue(
            string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    public static string ExtractMagicLinkToken(string? magicLink)
    {
        magicLink.ShouldNotBeNullOrWhiteSpace();
        var query = QueryHelpers.ParseQuery(new Uri(magicLink!).Query);
        query.TryGetValue("token", out var values).ShouldBeTrue("MagicLink did not contain a token query param.");
        var token = values.ToString();
        token.ShouldNotBeNullOrWhiteSpace();
        return token;
    }

    public static Guid? ReadVendorIdClaim(string accessToken)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        var raw = jwt.Claims.FirstOrDefault(c => c.Type == CustomClaims.VendorId)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// Buyer invite → anonymous resolve/save/submit → start-review → approve → set password → vendor client.
    /// </summary>
    public static async Task<OnboardedVendor> OnboardSwecVendorAsync(
        FshWebApplicationFactory factory,
        AuthHelper auth,
        HttpClient buyerClient,
        string? emailPrefix = null)
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var email = $"{emailPrefix ?? "vendor"}-{unique}@example.com";
        var companyName = $"Acme {unique}";
        var registrationNo = $"SSM-{unique}";

        // Anonymous draft calls can use any client; buyerClient is fine (token is the scope).
        using var inviteResponse = await buyerClient.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/invitations",
            new { email, type = "Swec", selectedTemplateIds = (string[]?)null });
        await EnsureSuccessAsync(inviteResponse, "Invite");
        var invitation = await inviteResponse.DeserializeAsync<OnboardingInvitationDto>();
        var token = ExtractMagicLinkToken(invitation.MagicLink);

        using var anonymous = factory.CreateClient();
        anonymous.DefaultRequestHeaders.Add("tenant", TestConstants.RootTenantId);

        using var resolveResponse = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/resolve",
            new { token });
        await EnsureSuccessAsync(resolveResponse, "Resolve");
        var application = await resolveResponse.DeserializeAsync<OnboardingApplicationDto>();

        using var draftResponse = await anonymous.PutAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft",
            new
            {
                token,
                name = companyName,
                registeredName = companyName,
                registrationNo,
                email,
                contactName = "Portal Contact",
                contactPhone = "+60123456789",
                region = "Central",
                state = "Selangor",
                city = "Shah Alam",
                countryCode = "MY"
            });
        await EnsureSuccessAsync(draftResponse, "Save draft");

        using var submitResponse = await anonymous.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/draft/submit",
            new { token });
        await EnsureSuccessAsync(submitResponse, "Submit draft");

        using var startReviewResponse = await buyerClient.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/start-review",
            new { });
        await EnsureSuccessAsync(startReviewResponse, "Start review");

        using var approveResponse = await buyerClient.PostAsJsonAsync(
            $"{TestConstants.SuppliersBasePath}/onboarding/applications/{application.Id}/approve",
            new { });
        await EnsureSuccessAsync(approveResponse, "Approve");
        var approved = await approveResponse.DeserializeAsync<OnboardingApproveResultDto>();

        await SetPasswordAsync(factory, email, DefaultVendorPassword);

        var vendorClient = await auth.CreateAuthenticatedClientAsync(email, DefaultVendorPassword);
        var tokenResult = await auth.GetTokenAsync(email, DefaultVendorPassword);
        var claimVendorId = ReadVendorIdClaim(tokenResult.AccessToken);
        claimVendorId.ShouldBe(approved.VendorId);

        return new OnboardedVendor(
            approved.VendorId,
            approved.VendorCode,
            email,
            DefaultVendorPassword,
            vendorClient,
            application.Id);
    }

    public static async Task EnsureSuccessAsync(HttpResponseMessage response, string step)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new InvalidOperationException($"{step} failed ({(int)response.StatusCode} {response.StatusCode}): {body}");
    }

    /// <summary>
    /// PR → reserve → RFQ draft (Dual|Single) → invite vendor(s) → release. Returns PR/RFQ ids and RFQ code.
    /// </summary>
    public static Task<(Guid PrId, Guid RfqId, string RfqCode)> CreateOpenRfqAsync(
        HttpClient buyer,
        Guid vendorId,
        string envelope = "Dual",
        string? memoPrefix = null,
        DateTime? closesUtc = null)
        => CreateOpenRfqAsync(buyer, [vendorId], envelope, memoPrefix, closesUtc);

    public static async Task<(Guid PrId, Guid RfqId, string RfqCode)> CreateOpenRfqAsync(
        HttpClient buyer,
        IReadOnlyList<Guid> vendorIds,
        string envelope = "Dual",
        string? memoPrefix = null,
        DateTime? closesUtc = null)
    {
        if (vendorIds is null || vendorIds.Count == 0)
        {
            throw new ArgumentException("At least one vendorId is required.", nameof(vendorIds));
        }

        var close = closesUtc ?? DateTime.UtcNow.AddDays(7);

        using var createPr = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/requisitions",
            new
            {
                requestor = "Integration Buyer",
                department = "Ops",
                departmentCode = (string?)null,
                location = "HQ",
                locationCode = (string?)null,
                category = "IT",
                categoryCode = (string?)null,
                job = "J1",
                jobCode = (string?)null,
                memo = $"{memoPrefix ?? "RFQ setup"} {Guid.NewGuid():N}"[..32],
                costCentre = "CC1",
                project = (string?)null,
                entryFormId = (Guid?)null,
                raisedOn = (DateOnly?)null,
                requiredOn = (DateOnly?)null,
                currency = "MYR",
                lines = new[] { new { itemCode = "ITEM-1", description = "Widget", qty = 10m, uom = "EA", estUnitPrice = 100m } },
                submit = true
            });
        await EnsureSuccessAsync(createPr, "Create PR");
        var prId = await createPr.DeserializeAsync<Guid>();

        using var getPr = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/requisitions/{prId}");
        await EnsureSuccessAsync(getPr, "Get PR");
        var pr = await getPr.DeserializeAsync<RequisitionDto>();
        var prLine = pr.Lines[0];

        using var reserve = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/requisitions/{prId}/lines/{prLine.Id}/reserve", new { });
        await EnsureSuccessAsync(reserve, "Reserve");

        using var createRfq = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs",
            new
            {
                title = $"RFQ {Guid.NewGuid():N}"[..24],
                envelope,
                currency = "MYR",
                prRefs = new[] { pr.Code },
                lines = new[]
                {
                    new
                    {
                        lineCode = "L1",
                        itemCode = "ITEM-1",
                        description = "Widget",
                        qty = 10m,
                        uom = "EA",
                        prRef = pr.Code,
                        sourcePrLineIds = new[] { prLine.Id.ToString() }
                    }
                }
            });
        await EnsureSuccessAsync(createRfq, "Create RFQ");
        var rfqId = await createRfq.DeserializeAsync<Guid>();

        using var update = await buyer.PutAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}",
            new
            {
                rfqId,
                title = $"RFQ {rfqId:N}"[..24],
                envelope,
                currency = "MYR",
                opensUtc = (DateTime?)null,
                closesUtc = close,
                lines = new[]
                {
                    new
                    {
                        lineCode = "L1",
                        itemCode = "ITEM-1",
                        description = "Widget",
                        qty = 10m,
                        uom = "EA",
                        prRef = pr.Code,
                        sourcePrLineIds = new[] { prLine.Id.ToString() }
                    }
                },
                formItems = Array.Empty<object>(),
                technicalSections = Array.Empty<string>(),
                commercialSections = Array.Empty<string>(),
                technicalEvaluatorIds = Array.Empty<string>(),
                commercialEvaluatorIds = Array.Empty<string>()
            });
        await EnsureSuccessAsync(update, "Update RFQ");

        foreach (var vendorId in vendorIds)
        {
            using var invite = await buyer.PostAsJsonAsync(
                $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/invitations",
                new { vendorId });
            await EnsureSuccessAsync(invite, $"Invite {vendorId}");
        }

        using var release = await buyer.PostAsJsonAsync(
            $"{TestConstants.SourcingBasePath}/rfqs/{rfqId}/release", new { });
        await EnsureSuccessAsync(release, "Release");

        using var getRfq = await buyer.GetAsync($"{TestConstants.SourcingBasePath}/rfqs/{rfqId}");
        await EnsureSuccessAsync(getRfq, "Get RFQ");
        var rfq = await getRfq.DeserializeAsync<RfqDetailDto>();
        return (prId, rfqId, rfq.Code);
    }
}

internal sealed record OnboardedVendor(
    Guid VendorId,
    string VendorCode,
    string Email,
    string Password,
    HttpClient Client,
    Guid ApplicationId);
