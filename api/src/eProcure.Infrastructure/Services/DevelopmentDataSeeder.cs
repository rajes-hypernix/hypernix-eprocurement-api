using eProcure.Application.Abstractions;
using eProcure.Domain;
using eProcure.Domain.Communication;
using eProcure.Domain.Files;
using eProcure.Domain.Identity;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using eProcure.Infrastructure.Persistence;
using eProcure.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace eProcure.Infrastructure.Services;

/// <summary>
/// Idempotent Development seeder (runs after migrations). Inserts the dummy demo
/// data from SEED-DATA.md so every screen is populated and walkable for the client
/// demo. No NetSuite. Internal users are never vendors (SoD); vendor logins are a
/// separate principal type.
/// </summary>
public sealed class DevelopmentDataSeeder(
    AppDbContext db,
    IClock clock,
    ILogger<DevelopmentDataSeeder> logger) : IDataSeeder
{
    private static readonly (string Code, string Name, string Email, string[] Roles)[] InternalUsers =
    [
        ("u_faridah", "Faridah Yusof",    "faridah@hypernix.test", [Roles.Buyer]),
        ("u_lim",     "Lim Chee Kong",    "lim@hypernix.test",     [Roles.Buyer, Roles.Approver]),
        ("u_hafiz",   "Ir. Hafiz Rahman", "hafiz@hypernix.test",   [Roles.TechEvaluator]),
        ("u_nur",     "Nurul Aina",       "nur@hypernix.test",     [Roles.TechEvaluator]),
        ("u_raj",     "Rajesh Kumar",     "raj@hypernix.test",     [Roles.TechEvaluator]),
        ("u_tan",     "Tan Mei Ling",     "tan@hypernix.test",     [Roles.CommEvaluator]),
        ("u_admin",   "System Admin",     "admin@hypernix.test",   [Roles.Admin]),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await SeedSwecAsync(ct);
        await SeedVendorsAsync(ct);
        await SeedUsersAsync(ct);
        await SeedVendorLoginsAsync(ct);
        await SeedRequisitionsAsync(ct);
        await SeedFormsAsync(ct);
        await SeedOnboardingFormsAsync(ct);
        await SeedCustomListsAsync(ct);
        await SeedRfqsAsync(ct);
        await SeedBidsAsync(ct);
        await SeedRfqGovernanceDemoAsync(ct);
        await SeedTechnicalScoresAsync(ct);
        await SeedAwardAsync(ct);
        await SeedPurchaseOrdersAsync(ct);
        await SeedDeliveriesAsync(ct);
        await SeedInvoicesAsync(ct);
        await SeedClarificationsAsync(ct);
        await SeedFilesAsync(ct);
        await SeedPrLineageDemoAsync(ct);
        await SeedNumberSequencesAsync(ct);
        logger.LogInformation("DataSeeder complete (Slice 1–8 master + sourcing + full P2P + clarifications).");
    }

    // One StoredFile per ownership class, carrying the TYPED ownership that FileAccessPolicy reads
    // (Slice G T5), so future migrations and the crawl exercise real file rows with real ownership
    // instead of an empty table. The Bid file points at a real submitted bid + its vendor (owner +
    // lineage); the two onboarding files point at a real vendor — the seed has no onboarding
    // application, so their OwnerEntityId is null (they are typed uploads, not reconstructed from the
    // pre-T5 answer-value convention); the Internal file is buyer/admin-only (fail-closed, no owner).
    private async Task SeedFilesAsync(CancellationToken ct)
    {
        if (await db.StoredFiles.AnyAsync(ct)) return;

        var now = clock.UtcNow;
        static StoredFile File(string name, FileOwnerKind kind, Guid? vendorId, Guid? entityId, DateTime at)
        {
            var content = System.Text.Encoding.UTF8.GetBytes($"seed {kind} file: {name}");
            return new StoredFile
            {
                Name = name, ContentType = "application/pdf", Content = content, Size = content.Length,
                CreatedUtc = at, OwnerKind = kind, OwnerVendorId = vendorId, OwnerEntityId = entityId,
            };
        }

        var files = new List<StoredFile>();

        var bid = await db.Bids.Where(b => b.Submitted).OrderBy(b => b.Code).FirstOrDefaultAsync(ct);
        if (bid is not null)
            files.Add(File("bid-technical-proposal.pdf", FileOwnerKind.Bid, bid.VendorId, bid.Id, now));

        var onbVendor = await db.Vendors.FirstOrDefaultAsync(v => v.Code == "SWK-V-11002", ct); // megatech
        if (onbVendor is not null)
        {
            files.Add(File("ssm-certificate.pdf", FileOwnerKind.OnboardingDocument, onbVendor.Id, null, now));
            files.Add(File("iso-9001-cert.pdf", FileOwnerKind.OnboardingAnswer, onbVendor.Id, null, now));
        }

        files.Add(File("buyer-scope-of-work.pdf", FileOwnerKind.Internal, null, null, now));

        db.StoredFiles.AddRange(files);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} StoredFiles (one per ownership class).", files.Count);
    }

    // Align the code sequences with the highest seeded code per (prefix, year) so the
    // first generated code never collides with a seeded one (e.g. FORM-2026-0002).
    private async Task SeedNumberSequencesAsync(CancellationToken ct)
    {
        var codes = new List<string>();
        codes.AddRange(await db.FormTemplates.AsNoTracking().Select(x => x.Code).ToListAsync(ct));
        codes.AddRange(await db.Rfqs.AsNoTracking().Select(x => x.Code).ToListAsync(ct));
        codes.AddRange(await db.PurchaseRequisitions.AsNoTracking().Select(x => x.Code).ToListAsync(ct));
        codes.AddRange(await db.Bids.AsNoTracking().Select(x => x.Code).ToListAsync(ct));
        codes.AddRange(await db.Awards.AsNoTracking().Select(x => x.Code).ToListAsync(ct));
        codes.AddRange(await db.PurchaseOrders.AsNoTracking().Select(x => x.Code).ToListAsync(ct));
        codes.AddRange(await db.Asns.AsNoTracking().Select(x => x.Code).ToListAsync(ct));
        codes.AddRange(await db.Grns.AsNoTracking().Select(x => x.Code).ToListAsync(ct));
        codes.AddRange(await db.Invoices.AsNoTracking().Select(x => x.Code).ToListAsync(ct));

        // Parse "PREFIX-YEAR-NNNN" → max NNNN per (prefix, year).
        var maxByKey = new Dictionary<(string Prefix, int Year), int>();
        foreach (var code in codes)
        {
            var parts = code?.Split('-');
            if (parts is not { Length: 3 }) continue;
            if (!int.TryParse(parts[1], out var year) || !int.TryParse(parts[2], out var n)) continue;
            var key = (parts[0].ToUpperInvariant(), year);
            maxByKey[key] = Math.Max(maxByKey.GetValueOrDefault(key), n);
        }

        foreach (var ((prefix, year), max) in maxByKey)
        {
            var seq = await db.NumberSequences.FirstOrDefaultAsync(s => s.Prefix == prefix && s.Year == year, ct);
            if (seq is null) { seq = new NumberSequence(prefix, year); db.NumberSequences.Add(seq); }
            seq.AdvanceTo(max);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedClarificationsAsync(CancellationToken ct)
    {
        if (await db.Clarifications.AnyAsync(ct)) return;
        var sentausa = await db.Vendors.FirstOrDefaultAsync(v => v.Code == "SWK-V-10293", ct);
        var hidro = await db.Vendors.FirstOrDefaultAsync(v => v.Code == "SWK-V-11150", ct);
        if (sentausa is null && hidro is null) return;
        var t = clock.UtcNow;

        if (hidro is not null)
        {
            // RFQ-specific thread: vendor question → buyer answer published to all bidders.
            db.Clarifications.Add(new Clarification { Scope = "RFQ-2026-0079", VendorId = hidro.Id, SenderKind = "vendor", SenderName = "Hidro Systems Sdn Bhd", Body = "Is a 3rd-party FAT witness required for the pumps, or is a factory FAT report acceptable?", CreatedUtc = t.AddHours(-26), ReadByBuyer = true, ReadByVendor = true });
            db.Clarifications.Add(new Clarification { Scope = "RFQ-2026-0079", VendorId = hidro.Id, SenderKind = "buyer", SenderName = "Faridah Yusof", Body = "A factory FAT report is acceptable; witnessed FAT is not required for this package.", Published = true, CreatedUtc = t.AddHours(-25), ReadByBuyer = true, ReadByVendor = false });
        }
        if (sentausa is not null)
        {
            // General inquiry thread with unread buyer message for the vendor.
            db.Clarifications.Add(new Clarification { Scope = "general", VendorId = sentausa.Id, SenderKind = "vendor", SenderName = "Sentausa Engineering Sdn Bhd", Body = "Could you confirm the bank details on file for our latest payment run?", CreatedUtc = t.AddHours(-5), ReadByBuyer = true, ReadByVendor = true });
            db.Clarifications.Add(new Clarification { Scope = "general", VendorId = sentausa.Id, SenderKind = "buyer", SenderName = "Faridah Yusof", Body = "Confirmed — remittance goes to the Maybank account ending 4471 on file. Let us know if that changes.", CreatedUtc = t.AddHours(-4), ReadByBuyer = true, ReadByVendor = false });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedSwecAsync(CancellationToken ct)
    {
        if (await db.SwecCategories.AnyAsync(ct)) return;
        db.SwecCategories.AddRange(SwecSeed.Build());
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedVendorsAsync(CancellationToken ct)
    {
        if (await db.Vendors.AnyAsync(ct)) return;
        foreach (var row in VendorSeed.Rows)
        {
            var v = VendorSeed.ToVendor(row);
            db.Vendors.Add(v);
            db.AuditEntries.Add(new AuditEntry("Vendor", v.Code, "Vendor record created",
                null, v.Name, "system", "System", v.CreatedUtc));
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedUsersAsync(CancellationToken ct)
    {
        if (await db.Users.AnyAsync(ct)) return;
        foreach (var (code, name, email, roles) in InternalUsers)
        {
            db.Users.Add(new User(code, name, email, roles)
            {
                CreatedUtc = clock.UtcNow,
                UpdatedUtc = clock.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedVendorLoginsAsync(CancellationToken ct)
    {
        if (await db.VendorUsers.AnyAsync(ct)) return;
        var vendors = await db.Vendors.ToListAsync(ct);
        foreach (var row in VendorSeed.Rows)
        {
            var vendor = vendors.FirstOrDefault(v => v.Code == row.Code);
            if (vendor is null) continue;
            db.VendorUsers.Add(new VendorUser(
                $"VU-{row.Slug}", vendor.Id, $"{row.Name} — Portal", $"vendor@{row.Slug}.test")
            {
                CreatedUtc = clock.UtcNow,
                UpdatedUtc = clock.UtcNow,
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedRequisitionsAsync(CancellationToken ct)
    {
        if (await db.PurchaseRequisitions.AnyAsync(ct)) return;
        db.PurchaseRequisitions.AddRange(SourcingSeed.Requisitions(clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedFormsAsync(CancellationToken ct)
    {
        if (await db.FormTemplates.AnyAsync(ct)) return;
        db.FormTemplates.AddRange(SourcingSeed.FormTemplates(clock.UtcNow));       // RFQ forms (Purpose=Rfq)
        await db.SaveChangesAsync(ct);
    }

    // Idempotent: adds the onboarding question packs if none exist yet — so a DB seeded before the
    // onboarding module (RFQ forms only) picks them up on the next startup.
    private async Task SeedOnboardingFormsAsync(CancellationToken ct)
    {
        if (await db.FormTemplates.AnyAsync(f => f.Purpose == FormPurpose.Onboarding, ct)) return;

        // Assign FORM codes AFTER the current max — a DB seeded before onboarding may already hold
        // user-created forms at 0003+, so the pack's default codes would collide on the unique index.
        var existing = await db.FormTemplates.AsNoTracking().Select(f => f.Code).ToListAsync(ct);
        var maxN = existing
            .Select(c => c.Split('-') is [.., _, var n] && int.TryParse(n, out var v) ? v : 0)
            .DefaultIfEmpty(0).Max();
        var year = clock.UtcNow.Year;

        var packs = OnboardingSeed.FormTemplates(clock.UtcNow);
        foreach (var p in packs) p.Code = $"FORM-{year}-{++maxN:D4}";
        db.FormTemplates.AddRange(packs);
        await db.SaveChangesAsync(ct);
    }

    // Idempotent: seeds the standard Custom Lists (country/state/city/currency/payment-terms/bank)
    // used by the manual New-Vendor form and the vendor self-service form (data-driven, not hardcoded).
    private async Task SeedCustomListsAsync(CancellationToken ct)
    {
        // Idempotent per list code so lists added in a later slice (e.g. the RFQ reason codes) land on
        // an already-seeded database, not only on a fresh one.
        var existing = await db.CustomLists.Select(l => l.Code).ToListAsync(ct);
        var missing = CustomListSeed.All(clock.UtcNow).Where(l => !existing.Contains(l.Code)).ToList();
        if (missing.Count == 0) return;
        db.CustomLists.AddRange(missing);
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Slice I demo data: gives one open RFQ a Declined + a Rescinded invitation (with reasons)
    /// and an Extended event (old→new close), plus flips bidders' invitations to BidSubmitted, so the
    /// Slice J timeline + status pills have real data. Idempotent (skips if the RFQ already has events).</summary>
    private async Task SeedRfqGovernanceDemoAsync(CancellationToken ct)
    {
        var rfq = await db.Rfqs.Include(r => r.Invitations)
            .Where(r => r.Status == RfqStatus.Open)
            .OrderBy(r => r.Code)
            .FirstOrDefaultAsync(ct);
        if (rfq is null || await db.RfqEvents.AnyAsync(e => e.RfqId == rfq.Id, ct)) return;

        var now = clock.UtcNow;
        var invitedVendorIds = rfq.Invitations.Select(i => i.VendorId).ToHashSet();

        // Top up with two fresh vendors from the master so we can show a Declined AND a Rescinded row
        // while keeping the live-invited set non-empty.
        var extra = await db.Vendors.Where(v => !invitedVendorIds.Contains(v.Id))
            .OrderBy(v => v.Code).Select(v => v.Id).Take(2).ToListAsync(ct);
        if (extra.Count < 2) return;

        var declineVendor = extra[0];
        var rescindVendor = extra[1];
        db.RfqInvitations.Add(RfqInvitation.Seed(rfq.Id, declineVendor, RfqInvitationStatus.Declined, now.AddHours(-8), declineReasonCode: "LEAD_TIME"));
        db.RfqEvents.Add(RfqEvent.Create(rfq.Id, RfqEventType.VendorDeclined, now.AddHours(-6),
            vendorId: declineVendor, actorVendorUserId: "vendor", reasonCode: "LEAD_TIME", reasonNote: "Cannot meet the delivery window."));

        db.RfqInvitations.Add(RfqInvitation.Seed(rfq.Id, rescindVendor, RfqInvitationStatus.Rescinded, now.AddHours(-8), rescindReasonCode: "DUPLICATE"));
        db.RfqEvents.Add(RfqEvent.Create(rfq.Id, RfqEventType.InvitationRescinded, now.AddHours(-4),
            vendorId: rescindVendor, actorUserId: rfq.OwnerUserId ?? "u_faridah", reasonCode: "DUPLICATE", reasonNote: "Invited in error."));

        var bidders = await db.Bids.Where(b => b.RfqId == rfq.Id && b.Submitted).Select(b => b.VendorId).ToListAsync(ct);

        // Extend the deadline forward, recording the old→new close.
        var oldCloses = rfq.ClosesUtc;
        var newCloses = (rfq.ClosesUtc ?? now).AddDays(5);
        rfq.OriginalClosesUtc ??= oldCloses;
        rfq.ClosesUtc = newCloses;
        db.RfqEvents.Add(RfqEvent.Create(rfq.Id, RfqEventType.Extended, now.AddHours(-2),
            actorUserId: rfq.OwnerUserId ?? "u_faridah", reasonCode: "LOW_RESPONSE", reasonNote: "Extended to gather more quotes.",
            oldClosesUtc: oldCloses, newClosesUtc: newCloses));

        // A Released event so the timeline starts cleanly.
        db.RfqEvents.Add(RfqEvent.Create(rfq.Id, RfqEventType.Released, rfq.OpensUtc ?? now.AddDays(-7), actorUserId: rfq.OwnerUserId ?? "u_faridah"));

        // Flip remaining bidders to BidSubmitted.
        foreach (var inv in rfq.Invitations)
            if (inv.Status == RfqInvitationStatus.Invited && bidders.Contains(inv.VendorId))
            {
                db.RfqInvitations.Remove(inv);
                db.RfqInvitations.Add(RfqInvitation.Seed(rfq.Id, inv.VendorId, RfqInvitationStatus.BidSubmitted, inv.InvitedUtc));
            }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// PR-module lifecycle showcase (PR-MODULE-SPEC §6): demo PRs covering every state plus a
    /// same-UoM merge pair and a different-UoM keep-separate pair for Consolidate Lines.
    /// </summary>
    private async Task SeedPrLineageDemoAsync(CancellationToken ct)
    {
        if (await db.PurchaseRequisitions.AnyAsync(p => p.Code == "PR-2026-0430", ct)) return;
        var now = clock.UtcNow;
        var ci = System.Globalization.CultureInfo.InvariantCulture;

        PurchaseRequisition Pr(string code, string memo, string dept, string cat, params PrLine[] lines)
            => PrWith(code, memo, dept, cat, submitted: true, lines);

        PurchaseRequisition PrWith(string code, string memo, string dept, string cat, bool submitted, params PrLine[] lines)
        {
            var pr = new PurchaseRequisition
            {
                Code = code, Requestor = "Demo Buyer", Memo = memo, CostCentre = "CC-DEMO", Currency = "MYR",
                Submitted = submitted,
                Department = dept, DepartmentCode = SourcingMapping.DimCode(dept),
                Category = cat, CategoryCode = SourcingMapping.DimCode(cat),
                Location = "Bintulu Plant", LocationCode = SourcingMapping.DimCode("Bintulu Plant"),
                Job = "JOB-DEMO", JobCode = SourcingMapping.DimCode("JOB-DEMO"),
                RaisedOn = DateOnly.FromDateTime(now), RaisedDate = now.ToString("dd/MM/yyyy", ci),
                RequiredOn = DateOnly.FromDateTime(now.AddDays(45)), RequiredDate = now.AddDays(45).ToString("dd/MM/yyyy", ci),
                Lines = [.. lines], CreatedUtc = now, UpdatedUtc = now,
            }.SeededAs(submitted ? "Approved" : "Draft");
            pr.RecomputeHeaderStatus();
            return pr;
        }

        // "No quotes" demo: an Open line that came back from an RFQ unsourced (Returned link).
        var nqLine = PrLine.Create("HSE-SIGN-A", "Safety signage set, type A", 30, "Set", 85m);
        nqLine.Ref = "RFQ-2026-0074";
        var pr430 = Pr("PR-2026-0430", "PPE restock — one line returned no-quote", "HSE", "Safety / PPE",
            PrLine.Create("HSE-EXT-9", "Fire extinguisher, 9 kg ABC", 20, "Unit", 450m), nqLine);
        var pr431 = Pr("PR-2026-0431", "Cancelled — duplicate request", "Maintenance", "Piping",
            PrLine.Create("PIP-OBS-1", "Obsolete pipe spec", 10, "Length", 300m, PrLineStatus.Cancelled),
            PrLine.Create("PIP-OBS-2", "Obsolete fitting", 5, "Unit", 90m, PrLineStatus.Cancelled));
        // Merge pair (GEN-MOTOR-55, same Unit) + keep-separate pair (GEN-CABLE-50, Meter vs Roll).
        var pr432 = Pr("PR-2026-0432", "Rotating spares — site A", "Production", "Rotating Equipment",
            PrLine.Create("GEN-MOTOR-55", "Electric Motor, 55 kW, IE3", 3, "Unit", 9200m),
            PrLine.Create("GEN-CABLE-50", "Power cable, 50 mm²", 200, "Meter", 45m));
        var pr433 = Pr("PR-2026-0433", "Rotating spares — site B", "Production", "Rotating Equipment",
            PrLine.Create("GEN-MOTOR-55", "Electric Motor, 55 kW, IE3", 2, "Unit", 9200m),
            PrLine.Create("GEN-CABLE-50", "Power cable, 50 mm² (drum)", 4, "Roll", 2300m));
        // A portal Draft PR: its Open lines are NOT source-eligible until it is submitted (§4.2).
        var pr434 = PrWith("PR-2026-0434", "Structural cabling works — draft", "Electrical", "Electrical", submitted: false,
            PrLine.Create("CBL-1002", "Armoured cable, 4-core, 25 mm²", 300, "Meter", 62m),
            PrLine.Create("CBL-1003", "Cable gland kit, 25 mm²", 40, "Set", 18m));

        db.PurchaseRequisitions.AddRange(pr430, pr431, pr432, pr433, pr434);
        await db.SaveChangesAsync(ct);

        // The no-quote line's Returned link (append-only) is what makes the "No quotes" chip derive.
        var rfq74 = await db.Rfqs.AsNoTracking().FirstOrDefaultAsync(r => r.Code == "RFQ-2026-0074", ct);
        if (rfq74 is not null)
        {
            var link = new PrLineSourcing(nqLine.Id, rfq74.Id, "HSE-SIGN-A", nqLine.Qty, now.AddDays(-6));
            link.MarkReturned("no vendor quoted this line", now.AddDays(-2));
            db.PrLineSourcings.Add(link);
            await db.SaveChangesAsync(ct);
        }
    }

    private async Task SeedRfqsAsync(CancellationToken ct)
    {
        if (await db.Rfqs.AnyAsync(ct)) return;

        // Map vendor seed slug -> persisted vendor Id (RFQs store vendor Ids).
        var vendors = await db.Vendors.ToListAsync(ct);
        var slugToId = VendorSeed.Rows.ToDictionary(
            r => r.Slug,
            r => vendors.FirstOrDefault(v => v.Code == r.Code)?.Id.ToString());

        var now = clock.UtcNow;
        foreach (var spec in SourcingSeed.RfqSpecs)
        {
            var rfq = SourcingSeed.ToRfq(spec, now, slug => slugToId.GetValueOrDefault(slug));
            db.Rfqs.Add(rfq);
            db.AuditEntries.Add(new AuditEntry("Rfq", rfq.Code, "RFQ seeded",
                null, rfq.Status.ToString(), "system", "System", rfq.CreatedUtc));
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedBidsAsync(CancellationToken ct)
    {
        if (await db.Bids.AnyAsync(ct)) return;

        var vendors = await db.Vendors.ToListAsync(ct);
        var slugToId = VendorSeed.Rows.ToDictionary(
            r => r.Slug, r => vendors.FirstOrDefault(v => v.Code == r.Code)?.Id);
        var rfqs = await db.Rfqs.ToListAsync(ct);
        var now = clock.UtcNow;

        foreach (var spec in SourcingSeed.BidSpecs)
        {
            var rfq = rfqs.FirstOrDefault(r => r.Code == spec.RfqCode);
            var vendorId = slugToId.GetValueOrDefault(spec.VendorSlug);
            if (rfq is null || vendorId is null) continue;

            var bid = SourcingSeed.ToBid(spec, rfq.Id, vendorId.Value, rfq, now);
            db.Bids.Add(bid);
            if (spec.Submitted)
                db.AuditEntries.Add(new AuditEntry("Bid", bid.Code, "Bid submitted",
                    null, $"RFQ {rfq.Code}", "system", vendors.First(v => v.Id == vendorId).Name, bid.SubmittedUtc ?? now));
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedTechnicalScoresAsync(CancellationToken ct)
    {
        if (await db.TechnicalScores.AnyAsync(ct)) return;

        var rfq = await db.Rfqs.FirstOrDefaultAsync(r => r.Code == "RFQ-2026-0079", ct);
        if (rfq is null) return;
        var vendors = await db.Vendors.ToListAsync(ct);
        Guid? Id(string code) => vendors.FirstOrDefault(v => v.Code == code)?.Id;

        // SEED-DATA §8: sentausa ≈ 88 PASS, hidro ≈ 82 PASS, megatech ≈ 58 FAIL (threshold 70).
        var targets = new (string Code, int Score)[]
        {
            ("SWK-V-10293", 88), // sentausa
            ("SWK-V-11150", 82), // hidro
            ("SWK-V-11002", 58), // megatech (FAIL)
        };
        var now = clock.UtcNow;
        foreach (var (code, value) in targets)
        {
            var vid = Id(code);
            if (vid is null) continue;
            foreach (var ev in rfq.TechnicalEvaluatorIds)
                foreach (var crit in TechnicalCriteria.All)
                    db.TechnicalScores.Add(new TechnicalScore(rfq.Id, vid.Value, ev, crit.Key, value) { UpdatedUtc = now });
        }

        rfq.TechnicalOpened = true;
        rfq.TechFinalized = true;
        db.AuditEntries.Add(new AuditEntry("Rfq", rfq.Code, "Technical finalized",
            null, "1 vendor(s) failed (threshold 70)", "system", "System", now));
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAwardAsync(CancellationToken ct)
    {
        if (await db.Awards.AnyAsync(ct)) return;

        // SEED-DATA §9: RFQ-0074 awarded to mutiara, approved by Lim (≠ buyer), one PO generated.
        var rfq = await db.Rfqs.FirstOrDefaultAsync(r => r.Code == "RFQ-2026-0074", ct);
        var mutiara = await db.Vendors.FirstOrDefaultAsync(v => v.Code == "SWK-V-10744", ct);
        if (rfq is null || mutiara is null) return;

        var now = clock.UtcNow;
        var allocations = new List<AwardAllocation>
        {
            new() { RfqLineCode = "HSE-HARN-2L", VendorId = mutiara.Id, Qty = 60, UnitPrice = 230m },
            new() { RfqLineCode = "HSE-GAS-4", VendorId = mutiara.Id, Qty = 15, UnitPrice = 3050m },
        };
        var award = new Award
        {
            Code = "AWD-2026-0074",
            RfqId = rfq.Id,
            CreatedByUserId = "u_faridah",
            ApproverUserId = "u_lim",     // different person (DoA / SoD)
            ApprovedUtc = now,
            Allocations = allocations,    // TotalValue (59,550) is derived from these (DBA-10)
            CreatedUtc = now,
            UpdatedUtc = now,
        }.SeededAs(AwardStatus.Approved);
        db.Awards.Add(award);

        var po = new PurchaseOrder
        {
            Code = "PO-2026-0074",
            VendorId = mutiara.Id,
            RfqId = rfq.Id,
            AwardCode = award.Code,
            AwardId = award.Id,                          // AN-2 lineage (coherent seed, matches AwardService)
            // Status defaults to Draft.
            NsId = "NS-PO-0074",
            CreatedUtc = now,
            UpdatedUtc = now,
            Lines = allocations.Select(a =>
            {
                var line = rfq.Lines.First(l => l.ItemCode == a.RfqLineCode);
                return new PoLine { AwardAllocationId = a.Id, ItemCode = a.RfqLineCode, Description = line.Description, Qty = a.Qty, Uom = line.Uom, UnitPrice = a.UnitPrice };
            }).ToList(),
        };
        db.PurchaseOrders.Add(po);

        db.AuditEntries.Add(new AuditEntry("Award", award.Code, "Award approved",
            "by u_faridah", "approved by u_lim · 1 PO: PO-2026-0074", "u_lim", "Lim Chee Kong", now));
        db.AuditEntries.Add(new AuditEntry("Po", po.Code, "PO generated from award", null, award.Code, "system", "System", now));
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedPurchaseOrdersAsync(CancellationToken ct)
    {
        if (await db.PurchaseOrders.AnyAsync(p => p.Code == "PO-2026-1185", ct)) return;
        var vendors = await db.Vendors.ToListAsync(ct);
        var now = clock.UtcNow;
        foreach (var row in ProcurementSeed.Pos)
        {
            var vendor = vendors.FirstOrDefault(v => v.Code == row.VendorCode);
            if (vendor is null) continue;
            var po = ProcurementSeed.ToPo(row, vendor.Id, now);
            db.PurchaseOrders.Add(po);
            db.AuditEntries.Add(new AuditEntry("Po", po.Code, "PO issued",
                null, $"{po.Status} · {po.NsId}", "system", "System", now));
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedDeliveriesAsync(CancellationToken ct)
    {
        if (await db.Asns.AnyAsync(a => a.Code == "ASN-2026-0508", ct)) return;
        var pos = await db.PurchaseOrders.ToListAsync(ct);
        var po1185 = pos.FirstOrDefault(p => p.Code == "PO-2026-1185");
        var po1186 = pos.FirstOrDefault(p => p.Code == "PO-2026-1186");
        if (po1185 is null || po1186 is null) return;
        var now = clock.UtcNow;

        // ASN-0508 fully received against PO-1185 (with GRN-0301).
        var asn0508 = new Asn
        {
            Code = "ASN-2026-0508", PoId = po1185.Id, VendorId = po1185.VendorId, Carrier = "Pos Logistics",
            TrackingNo = "PL-77310", ShippedDate = "12/06/2026", ExpectedDate = "14/06/2026",
            GrnCode = "GRN-2026-0301", CreatedUtc = now, UpdatedUtc = now,
            Lines =
            [
                new AsnLine { ItemCode = "MEP-PUMP-075", Description = "Centrifugal Pump, 75 kW, end-suction", ShippedQty = 4, Uom = "Unit", LotNo = "LOT-PMP-0612" },
                new AsnLine { ItemCode = "ELE-VFD-075", Description = "VFD Drive, 75 kW, IP55", ShippedQty = 4, Uom = "Unit", LotNo = "LOT-VFD-0612" },
            ],
        }.SeededAs(AsnStatus.Received);
        db.Asns.Add(asn0508);
        db.Grns.Add(new Grn
        {
            Code = "GRN-2026-0301", AsnId = asn0508.Id,
            PoId = po1185.Id, ReceivedDate = "14/06/2026", ReceivedBy = "Procurement", NsId = "NS-IR-30121", CreatedUtc = now,
            Lines =
            [
                new GrnLine { ItemCode = "MEP-PUMP-075", Description = "Centrifugal Pump, 75 kW, end-suction", ExpectedQty = 4, ReceivedQty = 4, Condition = "Good" },
                new GrnLine { ItemCode = "ELE-VFD-075", Description = "VFD Drive, 75 kW, IP55", ExpectedQty = 4, ReceivedQty = 4, Condition = "Good" },
            ],
        });
        // ASN-0511 in transit against PO-1186 (8 of the remaining gate valves).
        db.Asns.Add(new Asn
        {
            Code = "ASN-2026-0511", PoId = po1186.Id, VendorId = po1186.VendorId, Carrier = "Tiong Nam Logistics",
            TrackingNo = "TN-88421", ShippedDate = "26/06/2026", ExpectedDate = "29/06/2026",
            CreatedUtc = now, UpdatedUtc = now,   // Status defaults to InTransit
            Lines = [new AsnLine { ItemCode = "VLV-GAT-150", Description = "Gate Valve, DN150, PN16, CS", ShippedQty = 8, Uom = "Unit", LotNo = "LOT-VG-0626" }],
        });
        db.AuditEntries.Add(new AuditEntry("Asn", "ASN-2026-0508", "Goods receipt posted", null, "GRN-2026-0301", "system", "System", now));
        db.AuditEntries.Add(new AuditEntry("Asn", "ASN-2026-0511", "ASN submitted (in transit)", null, $"PO {po1186.Code}", "system", "Pantai Valve & Fitting", now));
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedInvoicesAsync(CancellationToken ct)
    {
        if (await db.Invoices.AnyAsync(i => i.Code == "INV-2026-0091", ct)) return;
        var pos = await db.PurchaseOrders.ToListAsync(ct);
        var po1185 = pos.FirstOrDefault(p => p.Code == "PO-2026-1185");
        var po1186 = pos.FirstOrDefault(p => p.Code == "PO-2026-1186");
        var po1193 = pos.FirstOrDefault(p => p.Code == "PO-2026-1193");
        if (po1185 is null || po1186 is null || po1193 is null) return;
        var now = clock.UtcNow;
        // PO-1185 has a single receipt (GRN-0301) → coherent 3-way-match lineage (Slice H T3).
        var grn1185 = await db.Grns.FirstOrDefaultAsync(g => g.PoId == po1185.Id, ct);

        // INV-0091: sentausa, PO-1185, fully matched → Paid (settled line for Statements).
        db.Invoices.Add(new Invoice
        {
            Code = "INV-2026-0091", PoId = po1185.Id, GrnId = grn1185?.Id, VendorId = po1185.VendorId, InvoiceNo = "STU-INV-3391",
            Date = "14/06/2026", NsId = "NS-VB-50121", CreatedUtc = now, UpdatedUtc = now,
            Lines =
            [
                new InvoiceLine { ItemCode = "MEP-PUMP-075", Description = "Centrifugal Pump, 75 kW, end-suction", Qty = 4, Uom = "Unit", UnitPrice = 48500 },
                new InvoiceLine { ItemCode = "ELE-VFD-075", Description = "VFD Drive, 75 kW, IP55", Qty = 4, Uom = "Unit", UnitPrice = 18200 },
            ],
        }.SeededAs(InvoiceStatus.Paid));
        // INV-0093: pantai, PO-1186, matched → Submitted (awaiting buyer approval).
        db.Invoices.Add(new Invoice
        {
            Code = "INV-2026-0093", PoId = po1186.Id, VendorId = po1186.VendorId, InvoiceNo = "PNT-INV-2207",
            Date = "27/06/2026", CreatedUtc = now, UpdatedUtc = now,
            Lines = [new InvoiceLine { ItemCode = "VLV-GAT-150", Description = "Gate Valve, DN150, PN16, CS", Qty = 16, Uom = "Unit", UnitPrice = 980 }],
        }.SeededAs(InvoiceStatus.Submitted));
        // INV-0094: megatech, PO-1193, price variance > 2% → Exception (blocked from payment).
        db.Invoices.Add(new Invoice
        {
            Code = "INV-2026-0094", PoId = po1193.Id, VendorId = po1193.VendorId, InvoiceNo = "MEG-INV-7720",
            Date = "24/06/2026", CreatedUtc = now, UpdatedUtc = now,
            ExceptionReason = "Unit price billed above PO (RM 23,200 vs RM 22,500).",
            Lines = [new InvoiceLine { ItemCode = "ELE-MTR-200", Description = "Motor, 200 kW, TEFC", Qty = 6, Uom = "Unit", UnitPrice = 23200 }],
        }.SeededAs(InvoiceStatus.Exception));

        db.AuditEntries.Add(new AuditEntry("Invoice", "INV-2026-0091", "Invoice approved & paid", null, "NS-VB-50121", "system", "System", now));
        db.AuditEntries.Add(new AuditEntry("Invoice", "INV-2026-0093", "Invoice submitted (matched)", null, $"PO {po1186.Code}", "system", "Pantai Valve & Fitting", now));
        db.AuditEntries.Add(new AuditEntry("Invoice", "INV-2026-0094", "Invoice submitted (exception)", null, "price variance > 2%", "system", "MegaTech Resources", now));
        await db.SaveChangesAsync(ct);
    }
}
