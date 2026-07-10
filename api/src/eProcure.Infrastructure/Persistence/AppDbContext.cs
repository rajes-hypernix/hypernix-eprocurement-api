using eProcure.Domain;
using eProcure.Domain.Communication;
using eProcure.Domain.Files;
using eProcure.Domain.Identity;
using eProcure.Domain.Onboarding;
using eProcure.Domain.Procurement;
using eProcure.Domain.Sourcing;
using eProcure.Domain.Suppliers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace eProcure.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<NumberSequence> NumberSequences => Set<NumberSequence>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<VendorUser> VendorUsers => Set<VendorUser>();
    public DbSet<SwecCategory> SwecCategories => Set<SwecCategory>();
    public DbSet<PurchaseRequisition> PurchaseRequisitions => Set<PurchaseRequisition>();
    public DbSet<PrLineSourcing> PrLineSourcings => Set<PrLineSourcing>();
    public DbSet<Rfq> Rfqs => Set<Rfq>();
    public DbSet<RfqInvitation> RfqInvitations => Set<RfqInvitation>();
    public DbSet<RfqEvent> RfqEvents => Set<RfqEvent>();
    public DbSet<FormTemplate> FormTemplates => Set<FormTemplate>();
    public DbSet<Bid> Bids => Set<Bid>();
    public DbSet<TechnicalScore> TechnicalScores => Set<TechnicalScore>();
    public DbSet<Award> Awards => Set<Award>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<Asn> Asns => Set<Asn>();
    public DbSet<Grn> Grns => Set<Grn>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<Clarification> Clarifications => Set<Clarification>();
    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();
    public DbSet<Domain.Configuration.CustomList> CustomLists => Set<Domain.Configuration.CustomList>();
    public DbSet<Domain.Configuration.CustomListValue> CustomListValues => Set<Domain.Configuration.CustomListValue>();

    // Vendor onboarding (Slice A) — staging is separate from the Vendor master.
    public DbSet<VendorOnboardingInvitation> VendorOnboardingInvitations => Set<VendorOnboardingInvitation>();
    public DbSet<VendorOnboardingApplication> VendorOnboardingApplications => Set<VendorOnboardingApplication>();
    public DbSet<VendorFinancialAssessment> VendorFinancialAssessments => Set<VendorFinancialAssessment>();
    public DbSet<OnboardingClarificationRound> OnboardingClarificationRounds => Set<OnboardingClarificationRound>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // List<string> <-> delimited string, with a value comparer so EF tracks edits.
        var listConverter = new ValueConverter<List<string>, string>(
            v => string.Join('|', v),
            s => s.Length == 0 ? new List<string>() : s.Split('|', StringSplitOptions.None).ToList());
        var listComparer = new ValueComparer<List<string>>(
            (a, c) => a!.SequenceEqual(c!),
            v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode())),
            v => v.ToList());

        // List<Guid> <-> delimited string (onboarding: selected FormTemplate ids).
        var guidListConverter = new ValueConverter<List<Guid>, string>(
            v => string.Join('|', v),
            s => s.Length == 0 ? new List<Guid>() : s.Split('|', StringSplitOptions.None).Select(Guid.Parse).ToList());
        var guidListComparer = new ValueComparer<List<Guid>>(
            (a, c) => a!.SequenceEqual(c!),
            v => v.Aggregate(0, (h, g) => HashCode.Combine(h, g.GetHashCode())),
            v => v.ToList());

        b.Entity<AuditEntry>(e =>
        {
            e.ToTable("AuditEntries");
            e.HasKey(x => x.Id);
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100).IsRequired();
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ActorName).HasMaxLength(200).IsRequired();
            e.Property(x => x.FromState).HasMaxLength(40);
            e.Property(x => x.ToState).HasMaxLength(40);
            e.Property(x => x.Reason).HasMaxLength(500);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.UtcTimestamp);
        });

        b.Entity<NumberSequence>(e =>
        {
            e.ToTable("NumberSequences");
            e.HasKey(x => new { x.Prefix, x.Year });
            e.Property(x => x.Prefix).HasMaxLength(20).IsRequired();
        });

        b.Entity<User>(e =>
        {
            e.ToTable("Users");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Roles).HasConversion(listConverter, listComparer);
        });

        b.Entity<Vendor>(e =>
        {
            e.ToTable("Vendors");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.RegisteredName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Categories).HasConversion(listConverter, listComparer);

            e.OwnsOne(x => x.Performance);
            e.OwnsMany(x => x.Contacts, o => o.ToTable("VendorContacts"));
            e.OwnsMany(x => x.Addresses, o => o.ToTable("VendorAddresses"));
            e.OwnsMany(x => x.BankAccounts, o => o.ToTable("VendorBankAccounts"));
            e.OwnsMany(x => x.Certifications, o => o.ToTable("VendorCertifications"));
            e.OwnsMany(x => x.Currencies, o => o.ToTable("VendorCurrencies"));
        });

        b.Entity<VendorUser>(e =>
        {
            e.ToTable("VendorUsers");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<SwecCategory>(e =>
        {
            e.ToTable("SwecCategories");
            e.HasKey(x => x.Code);
            e.Property(x => x.Code).HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.PathText).HasMaxLength(400).IsRequired();
        });

        b.Entity<PurchaseRequisition>(e =>
        {
            e.ToTable("PurchaseRequisitions");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.HeaderStatus).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Currency).HasMaxLength(10);
            e.Property(x => x.DepartmentCode).HasMaxLength(40);
            e.Property(x => x.LocationCode).HasMaxLength(40);
            e.Property(x => x.CategoryCode).HasMaxLength(40);
            e.Property(x => x.JobCode).HasMaxLength(40);
            e.Ignore(x => x.DerivedValue);                 // derived from lines; not persisted (§6)
            e.HasIndex(x => x.HeaderStatus);               // analytics filter (§10)
            e.HasIndex(x => x.RaisedOn);
            e.OwnsMany(x => x.Lines, o =>
            {
                o.ToTable("PrLines");
                o.HasKey(l => l.Id);                       // stable grain key (§7)
                o.Property(l => l.LifecycleStatus).HasConversion<string>().HasMaxLength(20);
                o.Property(l => l.ItemCode).HasMaxLength(50).IsRequired();
                o.Property(l => l.Uom).HasMaxLength(30);
                o.HasIndex(l => l.ItemCode);               // analytics: group by item
                o.HasIndex(l => l.LifecycleStatus);
            });
        });

        b.Entity<PrLineSourcing>(e =>
        {
            e.ToTable("PrLineSourcings");
            e.HasKey(x => x.Id);
            e.Property(x => x.RfqLineCode).HasMaxLength(60).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.LinkStatus).HasConversion<string>().HasMaxLength(20);
            // Reference-by-id across aggregates (no nav FK to owned PrLine); indexed for lineage joins.
            e.HasIndex(x => x.PrLineId);
            e.HasIndex(x => x.RfqId);
            e.HasIndex(x => x.RfqLineCode);
            e.HasIndex(x => x.LinkStatus);
            e.HasOne<Rfq>().WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);  // DBA-1
            // PrLineSourcing->PR deferred to Slice H: no PrId column, PrLine is owned (see BACKLOG).
        });

        b.Entity<Rfq>(e =>
        {
            e.ToTable("Rfqs");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Envelope).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PrRefs).HasConversion(listConverter, listComparer);
            e.Property(x => x.TechnicalEvaluatorIds).HasConversion(listConverter, listComparer);
            e.Property(x => x.CommercialEvaluatorIds).HasConversion(listConverter, listComparer);
            e.Property(x => x.TechnicalSections).HasConversion(listConverter, listComparer);
            e.Property(x => x.CommercialSections).HasConversion(listConverter, listComparer);
            e.OwnsMany(x => x.Lines, o =>
            {
                o.ToTable("RfqLines");
                o.Property(l => l.LineCode).HasMaxLength(60);   // lineage target (§2.4)
                o.Property(l => l.SourcePrLineIds).HasConversion(listConverter, listComparer);
            });
            e.OwnsMany(x => x.FormItems, o => o.ToTable("RfqFormItems"));
            // Invitations are a separate entity within the Rfq aggregate (own table + Vendor FK +
            // query indexes, so not OwnsMany). Restrict: never cascade-delete an invitation fact.
            e.HasMany(x => x.Invitations).WithOne().HasForeignKey(i => i.RfqId).OnDelete(DeleteBehavior.Restrict);
        });

        // RfqInvitation (RFQ-LIFECYCLE-ADDENDUM §2.1) — replaces Rfq.InvitedVendorIds.
        b.Entity<RfqInvitation>(e =>
        {
            e.ToTable("RfqInvitations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.DeclineReasonCode).HasMaxLength(40);
            e.Property(x => x.DeclineNote).HasMaxLength(500);
            e.Property(x => x.RescindReasonCode).HasMaxLength(40);
            e.Property(x => x.RescindNote).HasMaxLength(500);
            e.HasIndex(x => new { x.RfqId, x.VendorId, x.RoundNumber }).IsUnique();
            e.HasIndex(x => new { x.RfqId, x.Status });                       // buyer response-rate queries
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        });

        // RfqEvent (RFQ-LIFECYCLE-ADDENDUM §2.2) — append-only typed fact log.
        b.Entity<RfqEvent>(e =>
        {
            e.ToTable("RfqEvents");
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.ActorUserId).HasMaxLength(100);
            e.Property(x => x.ActorVendorUserId).HasMaxLength(100);
            e.Property(x => x.ReasonCode).HasMaxLength(40);
            e.Property(x => x.ReasonNote).HasMaxLength(500);
            e.HasIndex(x => new { x.RfqId, x.OccurredUtc });                  // timeline
            e.HasIndex(x => new { x.EventType, x.OccurredUtc });             // analytics
            e.HasOne<Rfq>().WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<FormTemplate>(e =>
        {
            e.ToTable("FormTemplates");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(20);   // SPEC §5, D1
            e.Property(x => x.TechnicalSections).HasConversion(listConverter, listComparer);
            e.Property(x => x.CommercialSections).HasConversion(listConverter, listComparer);
            e.HasIndex(x => x.Purpose);                    // Forms page / invite filter (D1)
            e.OwnsMany(x => x.Items, o => o.ToTable("FormTemplateItems"));
        });

        // ---- Vendor onboarding (Slice A) ----
        b.Entity<VendorOnboardingInvitation>(e =>
        {
            e.ToTable("VendorOnboardingInvitations");
            e.HasKey(x => x.Id);
            e.Property(x => x.Email).HasMaxLength(200).IsRequired();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();     // SHA-256 hex; raw never stored (F1)
            e.Property(x => x.InvitedByUserId).HasMaxLength(100).IsRequired();
            e.Property(x => x.InvitedByName).HasMaxLength(200).IsRequired();
            e.Property(x => x.SelectedTemplateIds).HasConversion(guidListConverter, guidListComparer);
            e.HasIndex(x => x.TokenHash).IsUnique();        // token resolve; one live token per hash (DBA-12)
            e.HasIndex(x => x.Status);                     // analytics/queue (§11)
            e.HasIndex(x => x.CreatedUtc);
            e.HasIndex(x => x.ApplicationId);
            e.HasIndex(x => x.InvitedByUserId);            // onboarding-throughput analytics: invitations per buyer (§11)
        });

        b.Entity<VendorOnboardingApplication>(e =>
        {
            e.ToTable("VendorOnboardingApplications");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Email).HasMaxLength(200);
            e.Property(x => x.RegistrationNo).HasMaxLength(60);
            e.Property(x => x.Categories).HasConversion(listConverter, listComparer);
            e.Property(x => x.SelectedTemplateIds).HasConversion(guidListConverter, guidListComparer);
            e.Property(x => x.RejectReason).HasMaxLength(1000);
            // Analytics/queue filters (§11): status, type, source, invited-by, and the cycle-time dates.
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Type);
            e.HasIndex(x => x.Source);
            e.HasIndex(x => x.InvitationId);
            e.HasIndex(x => x.CreatedUtc);
            e.HasIndex(x => x.SubmittedUtc);
            e.HasIndex(x => x.DecisionUtc);
            // Owned staging collections (same value objects as the master, for a clean promotion copy).
            e.OwnsMany(x => x.Contacts, o => o.ToTable("OnboardingContacts"));
            e.OwnsMany(x => x.Addresses, o => o.ToTable("OnboardingAddresses"));
            e.OwnsMany(x => x.BankAccounts, o => o.ToTable("OnboardingBankAccounts"));
            e.OwnsMany(x => x.Certifications, o => o.ToTable("OnboardingCertifications"));
            e.OwnsMany(x => x.Answers, o =>
            {
                o.ToTable("OnboardingAnswers");
                o.Property(a => a.Value).HasMaxLength(4000);
                o.HasIndex(a => a.FormTemplateId);
            });
            e.OwnsMany(x => x.Documents, o =>
            {
                o.ToTable("OnboardingDocuments");
                o.Property(d => d.Key).HasMaxLength(40);
                o.Property(d => d.FileName).HasMaxLength(260);
            });
            e.OwnsMany(x => x.Steps, o =>
            {
                o.ToTable("OnboardingReviewSteps");
                o.Property(s => s.Name).HasMaxLength(120);
                o.Property(s => s.Outcome).HasMaxLength(20);
                o.Property(s => s.Reason).HasMaxLength(1000);
                o.HasIndex(s => s.DecidedByUserId);        // analytics: decisions per approver (§11)
            });
            // Financial assessment (1:1) and clarification rounds (1:many) are owned by the aggregate.
            e.HasOne(x => x.Financial).WithOne()
                .HasForeignKey<VendorFinancialAssessment>(f => f.ApplicationId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.Rounds).WithOne()
                .HasForeignKey(r => r.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<VendorFinancialAssessment>(e =>
        {
            e.ToTable("VendorFinancialAssessments");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ApplicationId).IsUnique();
            e.Property(x => x.Remarks).HasMaxLength(4000);
            e.Ignore(x => x.LiveWeightedZ);                // derived; snapshot is what's stored (§6)
            e.OwnsMany(x => x.Years, o =>
            {
                o.ToTable("OnboardingFinancialYears");
                o.HasIndex(y => y.YearIndex);
            });
            e.OwnsMany(x => x.Snapshots, o =>
            {
                o.ToTable("OnboardingFinancialSnapshots");
                o.HasKey(s => s.Id);
                o.Property(s => s.Stage).HasConversion<string>().HasMaxLength(20);
                o.Property(s => s.Band).HasConversion<string>().HasMaxLength(5);
                o.Property(s => s.Risk).HasConversion<string>().HasMaxLength(20);
                o.Property(s => s.Statement).HasMaxLength(1000);
            });
        });

        b.Entity<OnboardingClarificationRound>(e =>
        {
            e.ToTable("OnboardingClarificationRounds");
            e.HasKey(x => x.Id);
            e.Property(x => x.Direction).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Message).HasMaxLength(4000);
            e.Property(x => x.RaisedByUserId).HasMaxLength(100);
            e.Property(x => x.RaisedByName).HasMaxLength(200);
            e.HasIndex(x => x.ApplicationId);
            e.HasIndex(x => new { x.ApplicationId, x.RoundNo });
            e.OwnsMany(x => x.Items, o =>
            {
                o.ToTable("OnboardingClarificationItems");
                o.HasKey(i => i.Id);
                o.Property(i => i.Topic).HasMaxLength(300);
                o.Property(i => i.Request).HasMaxLength(2000);
                o.Property(i => i.Response).HasMaxLength(2000);
            });
        });

        b.Entity<Bid>(e =>
        {
            e.ToTable("Bids");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.RfqId, x.VendorId }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.OwnsMany(x => x.Lines, o => o.ToTable("BidLines"));
            e.OwnsMany(x => x.Answers, o => o.ToTable("BidAnswers"));
            e.OwnsMany(x => x.Files, o => o.ToTable("BidAttachments"));
            // Referential integrity, no nav props (aggregate boundaries preserved) — DBA-1.
            e.HasOne<Rfq>().WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TechnicalScore>(e =>
        {
            e.ToTable("TechnicalScores");
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RfqId, x.VendorId, x.EvaluatorId, x.Criterion }).IsUnique();
            e.Property(x => x.EvaluatorId).HasMaxLength(50).IsRequired();
            e.Property(x => x.Criterion).HasMaxLength(30).IsRequired();
        });

        b.Entity<Award>(e =>
        {
            e.ToTable("Awards");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => x.RfqId).IsUnique();       // one award per RFQ (DBA-4)
            e.Ignore(x => x.TotalValue);               // derived from allocations, not persisted (DBA-10)
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.CreatedByUserId).HasMaxLength(50).IsRequired();
            e.HasOne<Rfq>().WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);  // DBA-1
            e.OwnsMany(x => x.Allocations, o =>
            {
                o.ToTable("AwardAllocations");
                o.HasOne<Vendor>().WithMany().HasForeignKey(a => a.VendorId).OnDelete(DeleteBehavior.Restrict);  // DBA-1
            });
        });

        b.Entity<PurchaseOrder>(e =>
        {
            e.ToTable("PurchaseOrders");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Total);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.OwnsMany(x => x.Lines, o => o.ToTable("PoLines"));
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);       // DBA-1
            e.HasOne<Rfq>().WithMany().HasForeignKey(x => x.RfqId).OnDelete(DeleteBehavior.Restrict);             // DBA-1 (RfqId nullable)
        });

        b.Entity<Asn>(e =>
        {
            e.ToTable("Asns");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.OwnsMany(x => x.Lines, o => o.ToTable("AsnLines"));
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PoId).OnDelete(DeleteBehavior.Restrict);    // DBA-1
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);       // DBA-1
        });

        b.Entity<Grn>(e =>
        {
            e.ToTable("Grns");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.OwnsMany(x => x.Lines, o => o.ToTable("GrnLines"));
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PoId).OnDelete(DeleteBehavior.Restrict);    // DBA-1
            e.HasOne<Asn>().WithMany().HasForeignKey(x => x.AsnId).OnDelete(DeleteBehavior.Restrict);             // DBA-1 (AsnId required — model is right)
        });

        b.Entity<Invoice>(e =>
        {
            e.ToTable("Invoices");
            e.HasKey(x => x.Id);
            e.Ignore(x => x.Subtotal);
            e.Ignore(x => x.Sst);
            e.Ignore(x => x.Wht);
            e.Ignore(x => x.Total);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(50).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.OwnsMany(x => x.Lines, o => o.ToTable("InvoiceLines"));
            e.HasOne<PurchaseOrder>().WithMany().HasForeignKey(x => x.PoId).OnDelete(DeleteBehavior.Restrict);    // DBA-1
            e.HasOne<Vendor>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);       // DBA-1
            // Invoice->Grn deferred to Slice H: Invoice has no GrnId column (see BACKLOG).
        });

        b.Entity<Clarification>(e =>
        {
            e.ToTable("Clarifications");
            e.HasKey(x => x.Id);
            e.Property(x => x.Scope).HasMaxLength(50).IsRequired();
            e.Property(x => x.SenderKind).HasMaxLength(10).IsRequired();
            e.Property(x => x.SenderName).HasMaxLength(120);
            e.Property(x => x.Body).HasMaxLength(4000);
            e.HasIndex(x => new { x.Scope, x.VendorId });
        });

        b.Entity<Domain.Configuration.CustomList>(e =>
        {
            e.ToTable("CustomLists");
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(60).IsRequired();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Description).HasMaxLength(400);
            e.Property(x => x.ParentListCode).HasMaxLength(60);
            e.HasMany(x => x.Values).WithOne().HasForeignKey(v => v.CustomListId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<Domain.Configuration.CustomListValue>(e =>
        {
            e.ToTable("CustomListValues");
            e.HasKey(x => x.Id);
            e.Property(x => x.Code).HasMaxLength(80).IsRequired();
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.ParentValueCode).HasMaxLength(80);
            e.HasIndex(x => x.CustomListId);
            e.HasIndex(x => new { x.CustomListId, x.Code }).IsUnique();   // conformed vocabulary: no dup codes per list
            e.HasIndex(x => new { x.CustomListId, x.ParentValueCode });   // dependent-list lookups
        });

        b.Entity<StoredFile>(e =>
        {
            e.ToTable("StoredFiles");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(260);
            e.Property(x => x.ContentType).HasMaxLength(120);
        });

        // Money convention: every decimal maps to numeric(18,2) (BUSINESS-RULES [$]).
        foreach (var prop in b.Model.GetEntityTypes()
                     .SelectMany(t => t.GetProperties())
                     .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
        {
            prop.SetColumnType("numeric(18,2)");
        }

        // Optimistic concurrency: use PostgreSQL's system column `xmin` as a row-version token on the
        // aggregate roots that carry a lifecycle. A stale write (e.g. two buyers extending one RFQ)
        // then raises DbUpdateConcurrencyException -> HTTP 409 instead of last-write-wins. The Npgsql
        // `UseXminAsConcurrencyToken` helper maps the existing system column, so this adds NO physical
        // column and needs no schema migration.
        foreach (var clr in new[]
                 {
                     typeof(Rfq), typeof(PurchaseRequisition), typeof(Award), typeof(PurchaseOrder),
                     typeof(Invoice), typeof(Asn), typeof(Grn), typeof(Vendor), typeof(VendorOnboardingApplication),
                 })
        {
            b.Entity(clr).Property<uint>("xmin")
                .HasColumnName("xmin")
                .HasColumnType("xid")
                .ValueGeneratedOnAddOrUpdate()
                .IsConcurrencyToken();
        }
    }
}
