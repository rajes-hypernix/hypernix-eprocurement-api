using System.Security.Cryptography;
using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Domain;
using eProcure.Application.Files;
using eProcure.Application.Onboarding;
using eProcure.Domain.Onboarding;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Onboarding invitation + magic-link use-cases (Slice B). At invite it creates the invitation
/// (hashed token) AND its staging application (status Invited, VOB code from <c>NumberSequence</c>),
/// emails the magic link via <see cref="IOnboardingNotifier"/>, and audits. The vendor's token
/// resolve opens the invitation and moves the application Invited → InProgress (A2). Nothing here
/// touches the Vendor master (staging is separate).
/// </summary>
public sealed class OnboardingService(
    AppDbContext db, IClock clock, ICodeGenerator codes, IAuditLog audit,
    IOnboardingNotifier notifier, ICurrentUser user, IFileStore files,
    IOptions<OnboardingOptions> options) : IOnboardingService
{
    private readonly OnboardingOptions _opt = options.Value;

    public async Task<IReadOnlyList<OnboardingTemplateDto>> ListOnboardingTemplatesAsync(CancellationToken ct = default)
    {
        var templates = await db.FormTemplates.AsNoTracking()
            .Where(f => f.Purpose == FormPurpose.Onboarding || f.Purpose == FormPurpose.Both)
            .OrderBy(f => f.Code).ToListAsync(ct);
        return [.. templates.Select(ToTemplateDto)];
    }

    public async Task<IReadOnlyList<OnboardingInvitationDto>> ListInvitationsAsync(CancellationToken ct = default)
    {
        var invites = await db.VendorOnboardingInvitations.AsNoTracking()
            .OrderByDescending(i => i.CreatedUtc).ToListAsync(ct);
        var codesById = await ApplicationCodesAsync(invites, ct);
        // The raw token is never stored, so a listed invite carries no reconstructable link.
        return [.. invites.Select(i => ToInvitationDto(i, codesById.GetValueOrDefault(i.ApplicationId ?? Guid.Empty), magicLink: ""))];
    }

    public async Task<OnboardingInvitationDto> CreateInvitationAsync(SendOnboardingInvitationRequest req, CancellationToken ct = default)
    {
        var email = string.IsNullOrWhiteSpace(req.Email) ? _opt.DefaultVendorEmail : req.Email.Trim();
        var type = ParseType(req.Type);
        var rawToken = NewRawToken();
        var now = clock.UtcNow;

        var invitation = VendorOnboardingInvitation.Create(
            email, type, req.SelectedTemplateIds, rawToken,
            user.UserId ?? "system", user.UserName ?? "System", now, _opt.LinkExpiryDays);

        var app = VendorOnboardingApplication.CreateFromInvitation(await codes.NextAsync(Domain.Views.RecordType.Onboarding, ct), invitation, now);
        if (!string.IsNullOrWhiteSpace(req.Name)) app.Name = req.Name!.Trim();
        invitation.AttachApplication(app.Id);

        db.VendorOnboardingInvitations.Add(invitation);
        db.VendorOnboardingApplications.Add(app);
        await db.SaveChangesAsync(ct);

        await audit.WriteTransitionAsync("VendorOnboardingInvitation", invitation.Id.ToString(),
            "Invitation sent", null, invitation.Status.ToString(), ct: ct);
        await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code,
            "Application created", null, app.Status.ToString(), ct: ct);

        await notifier.SendInvitationAsync(invitation, app.Code, rawToken, ct);

        return ToInvitationDto(invitation, app.Code, notifier.BuildMagicLink(rawToken));
    }

    public async Task<OnboardingInvitationDto> ResendInvitationAsync(Guid invitationId, CancellationToken ct = default)
    {
        var invitation = await LoadInvitation(invitationId, ct);
        var rawToken = NewRawToken();
        var t = invitation.Reissue(rawToken, clock.UtcNow, _opt.LinkExpiryDays);
        await db.SaveChangesAsync(ct);

        var app = await ApplicationOf(invitation, ct);
        await audit.WriteTransitionAsync("VendorOnboardingInvitation", invitation.Id.ToString(),
            t.Action, t.From.ToString(), t.To.ToString(), ct: ct);
        await notifier.SendInvitationAsync(invitation, app?.Code ?? "", rawToken, ct);

        return ToInvitationDto(invitation, app?.Code, notifier.BuildMagicLink(rawToken));
    }

    public async Task RevokeInvitationAsync(Guid invitationId, CancellationToken ct = default)
    {
        var invitation = await LoadInvitation(invitationId, ct);
        var t = invitation.Revoke(clock.UtcNow);

        var app = await ApplicationOf(invitation, ct);
        OnboardingTransition? appTransition = null;
        if (app is not null && app.Status is not (OnboardingStatus.Approved or OnboardingStatus.Rejected
            or OnboardingStatus.Expired or OnboardingStatus.Revoked or OnboardingStatus.Withdrawn))
        {
            appTransition = app.Revoke(clock.UtcNow);
        }
        await db.SaveChangesAsync(ct);

        await audit.WriteTransitionAsync("VendorOnboardingInvitation", invitation.Id.ToString(),
            t.Action, t.From.ToString(), t.To.ToString(), ct: ct);
        if (appTransition is { } at && app is not null)   // audit the application's transition too (every state change is audited)
            await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code, at.Action, at.From.ToString(), at.To.ToString(), ct: ct);
    }

    public async Task<OnboardingApplicationDto> ResolveTokenAsync(string rawToken, CancellationToken ct = default)
    {
        var hash = VendorOnboardingInvitation.HashToken(rawToken);
        var invitation = await db.VendorOnboardingInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct)
            ?? throw new NotFoundException("The onboarding link is not valid.");

        var app = await ApplicationOf(invitation, ct)
            ?? throw new NotFoundException("The onboarding application could not be found.");

        var now = clock.UtcNow;
        invitation.Open(rawToken, app.Id, now);                 // Sent → Opened; throws if expired/revoked
        if (app.Status == OnboardingStatus.Invited)
            app.MarkInProgress(now);                            // A2 — Invited → InProgress on first open
        await db.SaveChangesAsync(ct);

        await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code,
            "Onboarding link opened", null, app.Status.ToString(), ct: ct);

        return await ToApplicationDtoAsync(app, ct);
    }

    // ---- Slice C: the onboarding form (token-scoped) ----

    public async Task<OnboardingDraftDto> GetDraftAsync(string rawToken, CancellationToken ct = default) =>
        await ToDraftDtoAsync(await ResolveAppByToken(rawToken, ct), ct);

    public async Task<OnboardingDraftDto> SaveDraftAsync(SaveOnboardingDraftRequest req, CancellationToken ct = default)
    {
        var app = await ResolveAppByToken(req.Token, ct);
        RequireDraftable(app);

        if (req.Name is not null) app.Name = req.Name;
        if (req.RegistrationNo is not null) app.RegistrationNo = req.RegistrationNo;
        if (req.Location is not null) app.City = req.Location;
        if (req.Country is not null) app.Country = req.Country;      // conformed geography (Custom List codes)
        if (req.State is not null) app.State = req.State;
        if (req.Email is not null) app.Email = req.Email;
        if (req.ContactName is not null) app.ContactName = req.ContactName;
        if (req.ContactPhone is not null) app.ContactPhone = req.ContactPhone;
        if (req.Categories is not null) app.Categories = [.. req.Categories];

        // Owned collections are mutated in place (clear + add), not reassigned — the EF InMemory
        // provider raises a concurrency error when a loaded owned collection is replaced wholesale.
        if (req.Bank is not null)
        {
            app.BankAccounts.Clear();
            app.BankAccounts.Add(new VendorBankAccount { Bank = req.Bank.Bank, AccountNo = req.Bank.AccountNo, Swift = req.Bank.Swift, IsPrimary = true });
        }

        // Financials only apply to Non-SWEC (SWEC pre-qual is waived — B1); ignored otherwise.
        if (req.Financials is not null && app.Type == VendorType.NonSwec)
        {
            ValidateFinancialYears(req.Financials);          // exactly 3 years indexed 0/1/2 (guards the weighting)
            if (app.Financial is null)
            {
                app.Financial = new VendorFinancialAssessment(app.Id, req.Financials.Select(ToYearFigures), clock.UtcNow);
                db.VendorFinancialAssessments.Add(app.Financial);
            }
            else
            {
                app.Financial.Years.Clear();
                app.Financial.Years.AddRange(req.Financials.Select(ToYearFigures));
                app.Financial.UpdatedUtc = clock.UtcNow;
            }
        }

        if (req.Answers is not null)
        {
            app.Answers.Clear();
            app.Answers.AddRange(req.Answers.Select(a => new OnboardingAnswer { FormTemplateId = a.FormTemplateId, QuestionOrder = a.QuestionOrder, Value = a.Value }));
        }

        await db.SaveChangesAsync(ct);
        return await ToDraftDtoAsync(app, ct);
    }

    public async Task<OnboardingDraftDto> SubmitDraftAsync(string rawToken, CancellationToken ct = default)
    {
        var app = await ResolveAppByToken(rawToken, ct);
        if (string.IsNullOrWhiteSpace(app.Name) || string.IsNullOrWhiteSpace(app.RegistrationNo))
            throw new DomainRuleException("Complete the required company fields (registered name and SSM number) before submitting.");
        if (app.Type == VendorType.NonSwec)
        {
            if (app.Financial is null || app.Financial.Years.Count == 0)
                throw new DomainRuleException("Non-SWEC applications require three years of financial figures before submitting.");
            if (app.Financial.Years.Any(y => y.TotalAssets <= 0 || y.TotalLiabilities <= 0))
                throw new DomainRuleException("Each financial year needs positive total assets and total liabilities.");
        }

        var snapsBefore = app.Financial?.Snapshots.Count ?? 0;
        var t = app.Submit(clock.UtcNow);                        // InProgress → Submitted (+ snapshot, A3)
        // Explicit Added state on the new snapshot so EF's owned-collection tracking inserts the child
        // instead of mis-flagging its siblings (same InMemory workaround as RequisitionService).
        if (app.Financial is { } fin && fin.Snapshots.Count > snapsBefore)
            db.Entry(fin.Snapshots[^1]).State = EntityState.Added;
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code, t.Action,
            t.From.ToString(), t.To.ToString(), ct: ct);
        return await ToDraftDtoAsync(app, ct);
    }

    public async Task<OnboardingDocumentDto> UploadDocumentAsync(string rawToken, string key, string fileName,
        string contentType, byte[] content, CancellationToken ct = default)
    {
        var app = await ResolveAppByToken(rawToken, ct);
        RequireDraftable(app);
        var stored = await files.SaveAsync(fileName, contentType, content,
            FileOwnership.OnboardingDocument(app.Id), ct);   // owned by the onboarding application (T5)

        app.Documents.RemoveAll(d => d.Key == key);
        app.Documents.Add(new OnboardingDocument { Key = key, FileName = fileName, StoredFileId = stored.Id, UploadedUtc = clock.UtcNow });
        await db.SaveChangesAsync(ct);
        return new OnboardingDocumentDto(key, fileName, stored.Id);
    }

    public async Task DeleteDocumentAsync(string rawToken, string key, CancellationToken ct = default)
    {
        var app = await ResolveAppByToken(rawToken, ct);
        RequireDraftable(app);
        app.Documents.RemoveAll(d => d.Key == key);
        await db.SaveChangesAsync(ct);
    }

    // ---- Slice D: buyer review + clarification + approve/reject/promote ----

    public async Task<IReadOnlyList<OnboardingQueueItemDto>> ListApplicationsAsync(CancellationToken ct = default)
    {
        var apps = await db.VendorOnboardingApplications.AsNoTracking()
            .Include(a => a.Rounds)
            .Where(a => a.Status != OnboardingStatus.Draft)
            .OrderByDescending(a => a.CreatedUtc).ToListAsync(ct);
        return [.. apps.Select(a => new OnboardingQueueItemDto(
            a.Id, a.Code, a.Name, TypeLabel(a.Type), a.Status.ToString(), a.Source.ToString(),
            a.CreatedUtc, a.SubmittedUtc,
            a.Rounds.FirstOrDefault(r => r.Status == ClarificationRoundStatus.Open)?.RoundNo, a.Rounds.Count, a.InvitationId))];
    }

    // Read model: a fresh AsNoTracking load so producing the review never conflicts with a prior save.
    public async Task<OnboardingReviewDto> GetApplicationAsync(Guid id, CancellationToken ct = default)
    {
        var app = await db.VendorOnboardingApplications.AsNoTracking()
            .Include(a => a.Financial!).ThenInclude(f => f.Years)
            .Include(a => a.Rounds).ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new NotFoundException($"Onboarding application {id} not found.");
        return await ToReviewDtoAsync(app, ct);
    }

    public async Task<OnboardingReviewDto> StartReviewAsync(Guid id, CancellationToken ct = default)
    {
        var app = await LoadAppScalar(id, ct);                   // no Financial/Rounds — only the status changes
        var t = app.StartReview(clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code, t.Action, t.From.ToString(), t.To.ToString(), ct: ct);
        return await GetApplicationAsync(id, ct);
    }

    public async Task<OnboardingReviewDto> RequestClarificationAsync(Guid id, RequestClarificationRequest req, CancellationToken ct = default)
    {
        var app = await db.VendorOnboardingApplications.Include(a => a.Rounds)
            .FirstOrDefaultAsync(a => a.Id == id, ct) ?? throw new NotFoundException($"Onboarding application {id} not found.");
        var items = req.Items.Select(i => new OnboardingClarificationItem(i.Topic, i.Request)).ToList();
        var t = app.RequestClarification(ClarificationDirection.BuyerToVendor, req.Message,
            user.UserId ?? "system", user.UserName ?? "System", items, clock.UtcNow);
        MarkRoundAdded(app.Rounds[^1]);
        await db.SaveChangesAsync(ct);

        await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code, t.Action, t.From.ToString(), t.To.ToString(), t.Reason, ct);
        await notifier.SendClarificationAsync(app.Email, app.Code, req.Message,
            [.. items.Select(i => (i.Topic, i.Request))], ct);
        return await GetApplicationAsync(id, ct);
    }

    public async Task<OnboardingApplicationDto> ResubmitAsync(ResubmitOnboardingRequest req, CancellationToken ct = default)
    {
        var app = await ResolveAppByToken(req.Token, ct, includeFinancial: false);
        var t = app.Resubmit(req.Responses, clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code, t.Action, t.From.ToString(), t.To.ToString(), t.Reason, ct);
        return await ToApplicationDtoAsync(app, ct);
    }

    public async Task<OnboardingApplicationDto> RaiseClarificationAsync(RaiseClarificationRequest req, CancellationToken ct = default)
    {
        var app = await ResolveAppByToken(req.Token, ct, includeFinancial: false);
        var items = req.Items.Select(i => new OnboardingClarificationItem(i.Topic, i.Request)).ToList();
        var round = app.RaiseVendorClarification(req.Message, app.Name, items, clock.UtcNow);
        MarkRoundAdded(round);
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("VendorOnboardingApplication", app.Code, "Vendor raised clarification", after: $"Round {round.RoundNo}", ct: ct);
        return await ToApplicationDtoAsync(app, ct);
    }

    public async Task<OnboardingApproveResultDto> ApproveAsync(Guid id, CancellationToken ct = default)
    {
        var app = await LoadApp(id, ct);

        // Gates re-asserted BEFORE any master write or sequence consumption (defence over the state
        // machine): an illegal or replayed approval must never write to the master (F3, GOLDEN §2).
        if (app.Status != OnboardingStatus.UnderReview)
            throw new DomainRuleException($"Only an application under review can be approved (this one is {app.Status}).");
        if (app.Type == VendorType.NonSwec && app.Financial is not { Years.Count: > 0 })
            throw new DomainRuleException("A Non-SWEC application cannot be approved without a completed financial assessment.");

        var duplicate = await DuplicateWarningAsync(app, ct);
        var now = clock.UtcNow;

        // Build the master vendor in memory (Id assigned) — copies profile/banking/certs/categories,
        // and materialises the vendor's primary contact + address from the captured scalar fields
        // (the form stores those as scalars, not owned rows — so promotion must synthesise them).
        var vendorCode = await codes.NextAsync(Domain.Views.RecordType.Vendor, ct);
        var vendor = new Vendor
        {
            Code = vendorCode,
            Name = app.Name,
            RegisteredName = string.IsNullOrWhiteSpace(app.RegisteredName) ? app.Name : app.RegisteredName,
            RegistrationNo = string.IsNullOrWhiteSpace(app.RegistrationNo) ? "—" : app.RegistrationNo,
            TaxId = string.IsNullOrWhiteSpace(app.TaxId) ? "—" : app.TaxId,
            Type = app.Type,
            Region = app.Region, State = string.IsNullOrWhiteSpace(app.State) ? "—" : app.State,
            City = string.IsNullOrWhiteSpace(app.City) ? "—" : app.City, Country = app.Country,
            Categories = [.. app.Categories],
            Contacts = PromoteContacts(app),
            Addresses = PromoteAddresses(app),
            BankAccounts = [.. app.BankAccounts.Select(b => new VendorBankAccount { Bank = b.Bank, AccountNo = b.AccountNo, Swift = b.Swift, Currency = b.Currency, IsPrimary = b.IsPrimary })],
            Certifications = [.. app.Certifications.Select(c => new VendorCertification { Name = c.Name, Number = c.Number, ValidTo = c.ValidTo, Status = c.Status })],
            Currencies = [new VendorCurrency { Code = "MYR", IsPrimary = true }],
            CreatedUtc = now, UpdatedUtc = now,
        };
        // SWEC vendors are registered on promotion; Non-SWEC are provisional (SPEC §7).
        if (app.Type == VendorType.Swec) vendor.Register(); else vendor.MarkProvisional();
        var vendorUser = new VendorUser(await codes.NextAsync("VU", ct), vendor.Id, $"{app.Name} — Portal",
            await UniqueLoginEmailAsync(app.Email, vendorCode, ct)) { CreatedUtc = now, UpdatedUtc = now };

        // Domain guard runs here; only if it passes do we touch the master, then commit atomically.
        var snapsBefore = app.Financial?.Snapshots.Count ?? 0;
        var t = app.Approve(vendor.Id, user.UserId ?? "system", user.UserName ?? "System", now);

        db.Vendors.Add(vendor);
        db.VendorUsers.Add(vendorUser);
        if (app.Financial is not null) app.Financial.VendorId = vendor.Id;   // link the assessment to the master
        if (app.Financial is { } fin && fin.Snapshots.Count > snapsBefore)
            db.Entry(fin.Snapshots[^1]).State = EntityState.Added;
        if (app.InvitationId is { } invId)
        {
            var inv = await db.VendorOnboardingInvitations.FirstOrDefaultAsync(i => i.Id == invId, ct);
            inv?.Complete(now);
        }
        await db.SaveChangesAsync(ct);   // one flush: vendor + login + assessment link + snapshot + invitation

        await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code, t.Action, t.From.ToString(), t.To.ToString(), ct: ct);
        await audit.WriteAsync("Vendor", vendor.Code, "Vendor promoted from onboarding", after: vendor.Name, ct: ct);
        await audit.WriteAsync("VendorUser", vendorUser.Code, "Vendor login provisioned", ct: ct);

        var setPasswordLink = $"{_opt.PortalBaseUrl.TrimEnd('/')}/?vu={vendorUser.Id}#set-password";
        await notifier.SendApprovedAsync(vendorUser.Email, app.Code, vendor.Code, setPasswordLink, ct);
        return new OnboardingApproveResultDto(vendor.Id, vendor.Code, duplicate);
    }

    public async Task<OnboardingReviewDto> RejectAsync(Guid id, string reason, CancellationToken ct = default)
    {
        var app = await LoadAppScalar(id, ct);                   // no Financial/Rounds — only the status changes
        var t = app.Reject(reason, user.UserId ?? "system", user.UserName ?? "System", clock.UtcNow);
        await db.SaveChangesAsync(ct);
        await audit.WriteTransitionAsync("VendorOnboardingApplication", app.Code, t.Action, t.From.ToString(), t.To.ToString(), t.Reason, ct);
        await notifier.SendRejectedAsync(app.Email, app.Code, reason, ct);
        return await GetApplicationAsync(id, ct);
    }

    // ---- helpers ----

    private async Task<VendorOnboardingApplication> LoadApp(Guid id, CancellationToken ct) =>
        await db.VendorOnboardingApplications
            .Include(a => a.Financial!).ThenInclude(f => f.Years)
            .Include(a => a.Financial!).ThenInclude(f => f.Snapshots)
            .Include(a => a.Rounds).ThenInclude(r => r.Items)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException($"Onboarding application {id} not found.");

    // The onboarding form captures the primary contact + registered address as scalar fields; promotion
    // materialises them into the master's owned collections (or copies structured rows if present).
    private static List<VendorContact> PromoteContacts(VendorOnboardingApplication app)
    {
        if (app.Contacts.Count > 0)
            return [.. app.Contacts.Select(c => new VendorContact { Name = c.Name, Role = c.Role, Email = c.Email, Phone = c.Phone, IsPrimary = c.IsPrimary })];
        var hasContact = !string.IsNullOrWhiteSpace(app.ContactName) || !string.IsNullOrWhiteSpace(app.Email) || !string.IsNullOrWhiteSpace(app.ContactPhone);
        return hasContact
            ? [new VendorContact { Name = string.IsNullOrWhiteSpace(app.ContactName) ? app.Name : app.ContactName, Email = app.Email, Phone = app.ContactPhone, IsPrimary = true }]
            : [];
    }

    private static List<VendorAddress> PromoteAddresses(VendorOnboardingApplication app)
    {
        if (app.Addresses.Count > 0)
            return [.. app.Addresses.Select(a => new VendorAddress { Type = a.Type, Line = a.Line, City = a.City, State = a.State, Country = a.Country, Postcode = a.Postcode, IsPrimary = a.IsPrimary })];
        return string.IsNullOrWhiteSpace(app.City) && string.IsNullOrWhiteSpace(app.State)
            ? []
            : [new VendorAddress { Type = "Registered", City = app.City, State = app.State, Country = app.Country, IsPrimary = true }];
    }

    // Avoids the VendorUser unique-email collision (e.g. the same contact email onboarding twice).
    private async Task<string> UniqueLoginEmailAsync(string appEmail, string vendorCode, CancellationToken ct)
    {
        var fallback = $"{vendorCode.ToLowerInvariant()}@vendor.portal";
        var email = string.IsNullOrWhiteSpace(appEmail) ? fallback : appEmail;
        return await db.VendorUsers.AsNoTracking().AnyAsync(u => u.Email == email, ct) ? fallback : email;
    }

    private async Task<string?> DuplicateWarningAsync(VendorOnboardingApplication app, CancellationToken ct)
    {
        var reg = app.RegistrationNo;
        var dup = await db.Vendors.AsNoTracking().FirstOrDefaultAsync(v =>
            (reg != "" && reg != "—" && v.RegistrationNo == reg) || (app.Name != "" && v.Name == app.Name), ct);
        if (dup is null) return null;
        var field = dup.RegistrationNo == reg ? "registration number" : "name";
        return $"A vendor with this {field} already exists in the master ({dup.Code}).";
    }

    private async Task<OnboardingReviewDto> ToReviewDtoAsync(VendorOnboardingApplication app, CancellationToken ct)
    {
        var packs = await db.FormTemplates.AsNoTracking()
            .Where(f => app.SelectedTemplateIds.Contains(f.Id)).ToListAsync(ct);
        var bank = app.BankAccounts.FirstOrDefault();

        OnboardingFinancialViewDto? financial = null;
        if (app.Type == VendorType.NonSwec && app.Financial is { Years.Count: > 0 } fin)
        {
            var years = fin.Years.OrderBy(y => y.YearIndex).ToList();
            var z = AltmanZModel.Weighted(years);
            var band = AltmanZModel.BandFor(z);
            financial = new OnboardingFinancialViewDto(band.ToString(), AltmanZModel.RiskFor(band).ToString(),
                Zone(band), z, AltmanZModel.ScoreFor(z), AltmanZModel.StatementFor(band),
                [.. years.Select(y => { var c = AltmanZModel.Components(y); return new OnboardingFinancialYearCalcDto(y.YearIndex, c.X1, c.X2, c.X3, c.X4, c.X5, AltmanZModel.Z(y)); })]);
        }

        var answers = app.Answers.Select(a =>
        {
            var pack = packs.FirstOrDefault(p => p.Id == a.FormTemplateId);
            var item = pack?.Items.FirstOrDefault(i => i.Order == a.QuestionOrder);
            return new OnboardingAnswerViewDto(pack?.Name ?? "", item?.Label ?? $"Q{a.QuestionOrder}", a.Value);
        }).ToList();

        return new OnboardingReviewDto(
            app.Id, app.Code, app.Status.ToString(), TypeLabel(app.Type), app.Source.ToString(),
            app.Name, app.RegistrationNo, app.City, app.Email, app.ContactName, app.ContactPhone,
            new OnboardingBankDto(bank?.Bank ?? "", bank?.AccountNo ?? "", bank?.Swift ?? ""),
            [.. app.Categories], financial, [.. answers],
            [.. app.Documents.Select(d => new OnboardingDocumentDto(d.Key, d.FileName, d.StoredFileId))],
            [.. app.Rounds.OrderBy(r => r.RoundNo).Select(r => new OnboardingRoundDto(
                r.RoundNo, r.Direction.ToString(), r.Status.ToString(), r.Message, r.RaisedByName, r.RaisedUtc, r.RespondedUtc,
                [.. r.Items.Select(i => new OnboardingRoundItemDto(i.Topic, i.Request, i.Response))]))],
            await DuplicateWarningAsync(app, ct), app.SubmittedUtc, app.DecisionUtc, app.PromotedVendorId, app.RejectReason);
    }

    private static string Zone(FinancialBand band) => band switch
    {
        FinancialBand.A => "Safe zone", FinancialBand.B => "Safe / grey",
        FinancialBand.C => "Grey zone", _ => "Distress zone",
    };

    /// <summary>Resolves the application a live token belongs to (the access scope — F2). Financial owned
    /// collections are only loaded when the caller mutates them, to avoid an EF InMemory cross-context
    /// reconcile on an unrelated save.</summary>
    private async Task<VendorOnboardingApplication> ResolveAppByToken(string rawToken, CancellationToken ct, bool includeFinancial = true)
    {
        var hash = VendorOnboardingInvitation.HashToken(rawToken);
        var inv = await db.VendorOnboardingInvitations.FirstOrDefaultAsync(i => i.TokenHash == hash, ct)
            ?? throw new NotFoundException("The onboarding link is not valid.");
        if (inv.Status == OnboardingInvitationStatus.Revoked)
            throw new DomainRuleException("This onboarding invitation has been revoked.");
        if (inv.Status == OnboardingInvitationStatus.Expired || inv.IsExpired(clock.UtcNow))
            throw new DomainRuleException("This onboarding link has expired.");

        IQueryable<VendorOnboardingApplication> q = db.VendorOnboardingApplications
            .Include(a => a.Rounds).ThenInclude(r => r.Items);
        if (includeFinancial)
        {
            q = q.Include(a => a.Financial!).ThenInclude(f => f.Years);
            q = q.Include(a => a.Financial!).ThenInclude(f => f.Snapshots);
        }
        return await q.FirstOrDefaultAsync(a => a.Id == inv.ApplicationId, ct)
            ?? throw new NotFoundException("The onboarding application could not be found.");
    }

    /// <summary>Explicitly inserts a new clarification round + its items — EF InMemory otherwise
    /// mis-tracks a round added to a loaded nav collection (same workaround as new PR lines).</summary>
    private void MarkRoundAdded(OnboardingClarificationRound round)
    {
        db.Entry(round).State = EntityState.Added;
        foreach (var item in round.Items) db.Entry(item).State = EntityState.Added;
    }

    /// <summary>Loads an application for a status-only mutation (owned-on-app collections auto-load; the
    /// separate Financial / Rounds graphs are left untracked so the save has nothing to reconcile).</summary>
    private async Task<VendorOnboardingApplication> LoadAppScalar(Guid id, CancellationToken ct) =>
        await db.VendorOnboardingApplications.FirstOrDefaultAsync(a => a.Id == id, ct)
        ?? throw new NotFoundException($"Onboarding application {id} not found.");

    private static void RequireDraftable(VendorOnboardingApplication app)
    {
        if (app.Status is not (OnboardingStatus.Invited or OnboardingStatus.InProgress))
            throw new DomainRuleException($"This application is {app.Status} and can no longer be edited.");
    }

    // RM'000 — beyond any real company, and safely under the numeric(18,2) column limit (~1e16), so an
    // out-of-range figure (typically a typo / full-ringgit entry) returns a friendly error, never a 500.
    private const decimal FinancialFigureCap = 1_000_000_000_000m;

    private static void ValidateFinancialYears(IReadOnlyList<OnboardingFinancialYearDto> years)
    {
        var indices = years.Select(y => y.YearIndex).ToList();
        if (years.Count != 3 || indices.Distinct().Count() != 3 || indices.Any(i => i is < 0 or > 2))
            throw new DomainRuleException("Financials must have exactly three years indexed 0, 1 and 2.");

        static bool OutOfRange(OnboardingFinancialYearDto y) => new[]
        {
            y.Revenue, y.NetProfit, y.Ebit, y.TotalAssets, y.CurrentAssets, y.Inventory,
            y.CurrentLiabilities, y.TotalLiabilities, y.Equity, y.RetainedEarnings, y.FixedAssets,
        }.Any(v => Math.Abs(v) >= FinancialFigureCap);

        if (years.Any(OutOfRange))
            throw new DomainRuleException("A financial figure is out of range — enter amounts in RM'000 (thousands), not full ringgit.");
    }

    private static FinancialYearFigures ToYearFigures(OnboardingFinancialYearDto y) => new(y.YearIndex)
    {
        Revenue = y.Revenue, NetProfit = y.NetProfit, Ebit = y.Ebit, TotalAssets = y.TotalAssets,
        CurrentAssets = y.CurrentAssets, Inventory = y.Inventory, CurrentLiabilities = y.CurrentLiabilities,
        TotalLiabilities = y.TotalLiabilities, Equity = y.Equity, RetainedEarnings = y.RetainedEarnings,
        FixedAssets = y.FixedAssets,
    };

    private async Task<OnboardingDraftDto> ToDraftDtoAsync(VendorOnboardingApplication app, CancellationToken ct)
    {
        var packs = await db.FormTemplates.AsNoTracking()
            .Where(f => app.SelectedTemplateIds.Contains(f.Id)).OrderBy(f => f.Code).ToListAsync(ct);
        var bank = app.BankAccounts.FirstOrDefault();
        var band = app.Financial is { Years.Count: > 0 } fin
            ? AltmanZModel.BandFor(fin.LiveWeightedZ).ToString() : null;

        return new OnboardingDraftDto(
            app.Id, app.Code, app.Status.ToString(), TypeLabel(app.Type),
            app.Name, app.RegistrationNo, app.City, app.Email, app.ContactName, app.ContactPhone,
            new OnboardingBankDto(bank?.Bank ?? "", bank?.AccountNo ?? "", bank?.Swift ?? ""),
            [.. app.Categories],
            [.. (app.Financial?.Years ?? []).OrderBy(y => y.YearIndex).Select(ToYearDto)],
            [.. app.Answers.Select(a => new OnboardingAnswerDto(a.FormTemplateId, a.QuestionOrder, a.Value))],
            [.. app.Documents.Select(d => new OnboardingDocumentDto(d.Key, d.FileName, d.StoredFileId))],
            [.. packs.Select(ToPackDto)],
            band, app.SubmittedUtc, app.Country, app.State);
    }

    private static OnboardingFinancialYearDto ToYearDto(FinancialYearFigures y) => new(
        y.YearIndex, y.Revenue, y.NetProfit, y.Ebit, y.TotalAssets, y.CurrentAssets, y.Inventory,
        y.CurrentLiabilities, y.TotalLiabilities, y.Equity, y.RetainedEarnings, y.FixedAssets);

    private static OnboardingPackDto ToPackDto(FormTemplate f) => new(f.Id, f.Code, f.Name,
        [.. f.Items.OrderBy(i => i.Order).Select(i => new OnboardingFormItemDto(
            i.Kind, i.Group, i.Section, i.Label, i.Type, i.Required, i.ConfigJson, i.Help, i.Order))]);

    private async Task<VendorOnboardingInvitation> LoadInvitation(Guid id, CancellationToken ct) =>
        await db.VendorOnboardingInvitations.FirstOrDefaultAsync(i => i.Id == id, ct)
        ?? throw new NotFoundException($"Onboarding invitation {id} not found.");

    private Task<VendorOnboardingApplication?> ApplicationOf(VendorOnboardingInvitation inv, CancellationToken ct) =>
        inv.ApplicationId is { } appId
            ? db.VendorOnboardingApplications.FirstOrDefaultAsync(a => a.Id == appId, ct)
            : Task.FromResult<VendorOnboardingApplication?>(null);

    private async Task<Dictionary<Guid, string>> ApplicationCodesAsync(
        IEnumerable<VendorOnboardingInvitation> invites, CancellationToken ct)
    {
        var ids = invites.Where(i => i.ApplicationId is not null).Select(i => i.ApplicationId!.Value).Distinct().ToList();
        return await db.VendorOnboardingApplications.AsNoTracking()
            .Where(a => ids.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Code, ct);
    }

    private async Task<OnboardingApplicationDto> ToApplicationDtoAsync(VendorOnboardingApplication app, CancellationToken ct)
    {
        var packs = await db.FormTemplates.AsNoTracking()
            .Where(f => app.SelectedTemplateIds.Contains(f.Id)).ToListAsync(ct);
        var rounds = app.Rounds.OrderBy(r => r.RoundNo).Select(r => new OnboardingRoundDto(
            r.RoundNo, r.Direction.ToString(), r.Status.ToString(), r.Message, r.RaisedByName, r.RaisedUtc, r.RespondedUtc,
            [.. r.Items.Select(i => new OnboardingRoundItemDto(i.Topic, i.Request, i.Response))])).ToList();
        return new OnboardingApplicationDto(app.Id, app.Code, app.Status.ToString(), TypeLabel(app.Type),
            app.Name, app.Email, [.. packs.Select(ToTemplateDto)], [.. rounds], app.CreatedUtc, app.SubmittedUtc);
    }

    private static OnboardingTemplateDto ToTemplateDto(FormTemplate f) =>
        new(f.Id, f.Code, f.Name, f.Items.Count(i => i.Kind == "question"));

    private OnboardingInvitationDto ToInvitationDto(VendorOnboardingInvitation i, string? appCode, string magicLink) =>
        new(i.Id, i.Email, TypeLabel(i.Type), i.Status.ToString(), i.InvitedByName,
            i.CreatedUtc, i.ExpiresUtc, i.ApplicationId, appCode, magicLink);

    private static VendorType ParseType(string? type) =>
        (type ?? "").Trim().ToUpperInvariant() is "SWEC" or "PETRONAS SWEC" ? VendorType.Swec : VendorType.NonSwec;

    private static string TypeLabel(VendorType type) => type == VendorType.Swec ? "SWEC" : "Non-SWEC";

    /// <summary>A URL-safe, cryptographically-random raw token (only the hash is ever stored — F1).</summary>
    private static string NewRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(24))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
}
