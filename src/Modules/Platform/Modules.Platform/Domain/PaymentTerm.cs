using FSH.Framework.Core.Domain;

namespace FSH.Modules.Platform.Domain;

public enum PaymentTermKind
{
    Net = 0,
    DateDriven = 1,
    Schedule = 2,
}

public enum ScheduleBasis
{
    DaysFromDoc = 0,
    Advance = 1,
    MilestoneLabel = 2,
}

public sealed class PaymentTerm : AggregateRoot<Guid>, IAuditableEntity
{
    private readonly List<PaymentScheduleRow> _rows = [];

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public PaymentTermKind Kind { get; private set; }
    public bool IsActive { get; private set; } = true;

    public int? DueDays { get; private set; }
    public int? DayOfMonth { get; private set; }
    public int? MonthsAhead { get; private set; }
    public int? MinimumDaysBeforeDue { get; private set; }
    public decimal? DiscountPct { get; private set; }
    public int? DiscountDays { get; private set; }

    public IReadOnlyCollection<PaymentScheduleRow> Rows => _rows;

    public DateTimeOffset CreatedOnUtc { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? LastModifiedOnUtc { get; private set; }
    public string? LastModifiedBy { get; private set; }

    private PaymentTerm() { }

    public static PaymentTerm Create(
        string code,
        string name,
        PaymentTermKind kind,
        int? dueDays = null,
        int? dayOfMonth = null,
        int? monthsAhead = null,
        int? minimumDaysBeforeDue = null,
        decimal? discountPct = null,
        int? discountDays = null,
        IEnumerable<PaymentScheduleRowDraft>? rows = null,
        bool isActive = true,
        string? createdBy = null)
    {
        var term = new PaymentTerm
        {
            Id = Guid.CreateVersion7(),
            CreatedOnUtc = TimeProvider.System.GetUtcNow(),
            CreatedBy = createdBy,
        };
        term.Apply(
            code, name, kind, dueDays, dayOfMonth, monthsAhead, minimumDaysBeforeDue,
            discountPct, discountDays, rows, isActive, isCreate: true);
        return term;
    }

    public void Update(
        string name,
        PaymentTermKind kind,
        int? dueDays,
        int? dayOfMonth,
        int? monthsAhead,
        int? minimumDaysBeforeDue,
        decimal? discountPct,
        int? discountDays,
        IEnumerable<PaymentScheduleRowDraft>? rows,
        string? modifiedBy = null)
    {
        Apply(
            Code, name, kind, dueDays, dayOfMonth, monthsAhead, minimumDaysBeforeDue,
            discountPct, discountDays, rows, IsActive, isCreate: false);
        Touch(modifiedBy);
    }

    public void SetActive(bool isActive, string? modifiedBy = null)
    {
        IsActive = isActive;
        Touch(modifiedBy);
    }

    private void Apply(
        string code,
        string name,
        PaymentTermKind kind,
        int? dueDays,
        int? dayOfMonth,
        int? monthsAhead,
        int? minimumDaysBeforeDue,
        decimal? discountPct,
        int? discountDays,
        IEnumerable<PaymentScheduleRowDraft>? rows,
        bool isActive,
        bool isCreate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (isCreate)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(code);
            var normalized = code.Trim().ToUpperInvariant();
            if (normalized.Length > 20)
                throw new PlatformRuleException("Payment term code must be at most 20 characters.");
            Code = normalized;
        }

        Name = name.Trim();
        if (Name.Length > 80)
            throw new PlatformRuleException("Payment term name must be at most 80 characters.");

        Kind = kind;
        IsActive = isActive;
        DueDays = null;
        DayOfMonth = null;
        MonthsAhead = null;
        MinimumDaysBeforeDue = null;
        DiscountPct = null;
        DiscountDays = null;
        _rows.Clear();

        switch (kind)
        {
            case PaymentTermKind.Net:
                if (dueDays is null or < 0)
                    throw new PlatformRuleException("Net payment terms require DueDays ≥ 0.");
                DueDays = dueDays;
                ApplyDiscount(discountPct, discountDays);
                break;
            case PaymentTermKind.DateDriven:
                if (dayOfMonth is null or < 1 or > 31)
                    throw new PlatformRuleException("DateDriven terms require DayOfMonth between 1 and 31.");
                if (monthsAhead is null or < 0)
                    throw new PlatformRuleException("DateDriven terms require MonthsAhead ≥ 0.");
                DayOfMonth = dayOfMonth;
                MonthsAhead = monthsAhead;
                MinimumDaysBeforeDue = minimumDaysBeforeDue is < 0
                    ? throw new PlatformRuleException("MinimumDaysBeforeDue cannot be negative.")
                    : minimumDaysBeforeDue;
                ApplyDiscount(discountPct, discountDays);
                break;
            case PaymentTermKind.Schedule:
                if (discountPct is not null || discountDays is not null)
                    throw new PlatformRuleException("Schedule payment terms cannot carry an early-pay discount.");
                var drafts = (rows ?? []).OrderBy(r => r.Seq).ToList();
                if (drafts.Count == 0)
                    throw new PlatformRuleException("Schedule payment terms require at least one row.");
                var sum = drafts.Sum(r => r.Percent);
                if (sum != 100m)
                    throw new PlatformRuleException($"Schedule row percents must sum to exactly 100 (got {sum}).");
                foreach (var d in drafts)
                    _rows.Add(PaymentScheduleRow.Create(d.Seq, d.Percent, d.Basis, d.Days, d.Label));
                break;
            default:
                throw new PlatformRuleException($"Unknown payment term kind '{kind}'.");
        }
    }

    private void ApplyDiscount(decimal? discountPct, int? discountDays)
    {
        if (discountPct is null && discountDays is null) return;
        if (discountPct is null || discountDays is null)
            throw new PlatformRuleException("Early-pay discount requires both DiscountPct and DiscountDays.");
        if (discountPct is < 0 or > 100)
            throw new PlatformRuleException("DiscountPct must be between 0 and 100.");
        if (discountDays < 0)
            throw new PlatformRuleException("DiscountDays cannot be negative.");
        DiscountPct = discountPct;
        DiscountDays = discountDays;
    }

    private void Touch(string? modifiedBy)
    {
        LastModifiedOnUtc = TimeProvider.System.GetUtcNow();
        LastModifiedBy = modifiedBy;
    }
}

public sealed class PaymentScheduleRow
{
    public Guid Id { get; private set; }
    public int Seq { get; private set; }
    public decimal Percent { get; private set; }
    public ScheduleBasis Basis { get; private set; }
    public int? Days { get; private set; }
    public string? Label { get; private set; }

    private PaymentScheduleRow() { }

    internal static PaymentScheduleRow Create(int seq, decimal percent, ScheduleBasis basis, int? days, string? label)
    {
        if (seq < 1) throw new PlatformRuleException("Schedule Seq must be ≥ 1.");
        if (percent is <= 0 or > 100) throw new PlatformRuleException("Schedule Percent must be between 0 and 100.");
        if (basis == ScheduleBasis.DaysFromDoc && days is null)
            throw new PlatformRuleException($"Schedule row {seq} is DaysFromDoc but missing Days.");
        if (basis == ScheduleBasis.MilestoneLabel && string.IsNullOrWhiteSpace(label))
            throw new PlatformRuleException($"Schedule row {seq} is MilestoneLabel but missing Label.");

        return new PaymentScheduleRow
        {
            Id = Guid.CreateVersion7(),
            Seq = seq,
            Percent = percent,
            Basis = basis,
            Days = basis == ScheduleBasis.DaysFromDoc ? days : null,
            Label = string.IsNullOrWhiteSpace(label) ? null : label.Trim(),
        };
    }
}

public sealed record PaymentScheduleRowDraft(
    int Seq,
    decimal Percent,
    ScheduleBasis Basis,
    int? Days = null,
    string? Label = null);
