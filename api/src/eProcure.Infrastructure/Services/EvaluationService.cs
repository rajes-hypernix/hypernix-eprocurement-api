using eProcure.Application;
using eProcure.Application.Abstractions;
using eProcure.Application.Sourcing;
using eProcure.Domain;
using eProcure.Domain.Identity;
using eProcure.Domain.Sourcing;
using eProcure.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace eProcure.Infrastructure.Services;

public sealed class EvaluationService(
    AppDbContext db,
    IClock clock,
    IAuditLog audit,
    ICurrentUser user) : IEvaluationService
{
    public async Task<BidOpeningDto?> GetOpeningAsync(Guid rfqId, CancellationToken ct = default)
    {
        var rfq = await db.Rfqs.AsNoTracking().Include(r => r.Invitations).FirstOrDefaultAsync(r => r.Id == rfqId, ct);
        return rfq is null ? null : await OpeningDto(rfq, ct);
    }

    public async Task<BidOpeningDto> OpenTechnicalAsync(Guid rfqId, CancellationToken ct = default)
    {
        EnsureInternal();
        var rfq = await Load(rfqId, ct);
        if (!rfq.TechnicalEvaluatorIds.Contains(user.UserId ?? ""))
            throw new ForbiddenException("Only an assigned technical evaluator can open the technical envelope.");
        if (rfq.Envelope != RfqEnvelope.Dual)
            throw new DomainRuleException("Only dual-envelope RFQs have a sealed technical envelope.");
        if (rfq.Status is RfqStatus.Draft or RfqStatus.Open)
            throw new DomainRuleException("Close the bid window before opening envelopes.");
        rfq.TechnicalOpened = true;
        if (rfq.Status == RfqStatus.Closed) rfq.Status = RfqStatus.Evaluation;
        rfq.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "Technical envelope opened", ct: ct);
        return await OpeningDto(rfq, ct);
    }

    public async Task<BidOpeningDto> OpenCommercialAsync(Guid rfqId, CancellationToken ct = default)
    {
        EnsureInternal();
        var rfq = await Load(rfqId, ct);
        // [G] sealed bids: commercial may be opened only after technical is finalized,
        // and only by an assigned commercial evaluator.
        if (rfq.Envelope == RfqEnvelope.Dual && !rfq.TechFinalized)
            throw new DomainRuleException("Finalize technical scoring before opening the commercial envelope.");
        // Dual envelopes gate commercial opening to assigned commercial evaluators (SoD);
        // single-envelope RFQs have no sealed split, so the buyer opens them.
        if (rfq.Envelope == RfqEnvelope.Dual && !rfq.CommercialEvaluatorIds.Contains(user.UserId ?? ""))
            throw new ForbiddenException("Only an assigned commercial evaluator can open the commercial envelope.");
        rfq.CommercialOpened = true;
        if (rfq.Status == RfqStatus.Closed) rfq.Status = RfqStatus.Evaluation;
        rfq.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);
        await audit.WriteAsync("Rfq", rfq.Code, "Commercial envelope opened", ct: ct);
        return await OpeningDto(rfq, ct);
    }

    public async Task<TechnicalEvalDto?> GetTechnicalEvalAsync(Guid rfqId, CancellationToken ct = default)
    {
        var rfq = await db.Rfqs.AsNoTracking().Include(r => r.Invitations).FirstOrDefaultAsync(r => r.Id == rfqId, ct);
        return rfq is null ? null : await EvalDto(rfq, ct);
    }

    public async Task<TechnicalEvalDto> SetScoreAsync(Guid rfqId, SetScoreRequest req, CancellationToken ct = default)
    {
        EnsureInternal();
        var rfq = await Load(rfqId, ct);
        if (rfq.TechFinalized)
            throw new DomainRuleException("Technical scoring is finalized and can no longer be changed.");
        if (!rfq.TechnicalEvaluatorIds.Contains(req.EvaluatorId))
            throw new DomainRuleException($"{req.EvaluatorId} is not an assigned technical evaluator for this RFQ.");
        if (!TechnicalCriteria.IsValidKey(req.Criterion))
            throw new DomainRuleException($"Unknown criterion '{req.Criterion}'.");

        var score = Math.Clamp(req.Score, 0, 100);
        var existing = await db.TechnicalScores.FirstOrDefaultAsync(
            s => s.RfqId == rfqId && s.VendorId == req.VendorId && s.EvaluatorId == req.EvaluatorId && s.Criterion == req.Criterion, ct);
        if (existing is null)
            db.TechnicalScores.Add(new TechnicalScore(rfqId, req.VendorId, req.EvaluatorId, req.Criterion, score) { CreatedUtc = clock.UtcNow, UpdatedUtc = clock.UtcNow });
        else { existing.Score = score; existing.UpdatedUtc = clock.UtcNow; }

        await db.SaveChangesAsync(ct);
        return await EvalDto(rfq, ct);
    }

    public async Task<TechnicalEvalDto> FinalizeTechnicalAsync(Guid rfqId, CancellationToken ct = default)
    {
        EnsureInternal();
        var rfq = await Load(rfqId, ct);
        if (!rfq.TechnicalOpened)
            throw new DomainRuleException("Open the technical envelope before finalizing.");

        var submitted = await SubmittedVendorIds(rfqId, ct);
        if (submitted.Count == 0)
            throw new DomainRuleException("No submitted bids to finalize.");
        var scores = await db.TechnicalScores.AsNoTracking().Where(s => s.RfqId == rfqId).ToListAsync(ct);
        var allScored = submitted.All(v =>
            TechnicalEvaluation.Committee(scores, v, rfq.TechnicalEvaluatorIds) is not null);
        if (!allScored)
            throw new DomainRuleException("Each vendor needs at least one evaluator scored on all criteria.");

        rfq.TechFinalized = true;
        rfq.UpdatedUtc = clock.UtcNow;
        await db.SaveChangesAsync(ct);

        var failed = submitted.Count(v => !TechnicalEvaluation.Pass(TechnicalEvaluation.Committee(scores, v, rfq.TechnicalEvaluatorIds)));
        await audit.WriteAsync("Rfq", rfq.Code, "Technical finalized",
            after: $"{failed} vendor(s) failed (threshold {TechnicalCriteria.Threshold})", ct: ct);
        return await EvalDto(rfq, ct);
    }

    // ---- helpers ----

    /// <summary>Masking is derived from the principal's roles, not any client flag, so it
    /// cannot be defeated by switching a value in the request (BUSINESS-RULES [G]).</summary>
    private bool Masked()
    {
        var roles = user.Roles;
        var isEvaluator = roles.Contains(Roles.TechEvaluator) || roles.Contains(Roles.CommEvaluator);
        var isPrivileged = roles.Contains(Roles.Buyer) || roles.Contains(Roles.Admin);
        return isEvaluator && !isPrivileged;
    }

    private void EnsureInternal()
    {
        if (user.VendorId is not null)
            throw new ForbiddenException("Vendor users cannot open or score bids.");
    }

    private async Task<Rfq> Load(Guid id, CancellationToken ct) =>
        await db.Rfqs.FirstOrDefaultAsync(r => r.Id == id, ct)
        ?? throw new NotFoundException($"RFQ {id} not found.");

    private async Task<List<Guid>> SubmittedVendorIds(Guid rfqId, CancellationToken ct)
    {
        var rfq = await db.Rfqs.AsNoTracking().Include(r => r.Invitations).FirstAsync(r => r.Id == rfqId, ct);
        var submitted = await db.Bids.AsNoTracking()
            .Where(b => b.RfqId == rfqId && b.Submitted).Select(b => b.VendorId).ToListAsync(ct);
        // order by invited position so aliases are stable A,B,C
        return SourcingMapping.LiveInvitedVendorIds(rfq)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => submitted.Contains(g)).ToList();
    }

    private async Task<BidOpeningDto> OpeningDto(Rfq rfq, CancellationToken ct)
    {
        var invited = SourcingMapping.LiveInvitedVendorIds(rfq).Count;
        var submitted = await db.Bids.CountAsync(b => b.RfqId == rfq.Id && b.Submitted, ct);
        var me = user.UserId ?? "";
        var canTech = rfq.Envelope == RfqEnvelope.Dual && !rfq.TechnicalOpened
            && rfq.Status is not (RfqStatus.Draft or RfqStatus.Open)
            && rfq.TechnicalEvaluatorIds.Contains(me);   // [G] assigned tech evaluators only
        var canComm = !rfq.CommercialOpened
            && (rfq.Envelope == RfqEnvelope.Single
                ? rfq.Status is not (RfqStatus.Draft or RfqStatus.Open)        // single: buyer opens
                : rfq.TechFinalized && rfq.CommercialEvaluatorIds.Contains(me)); // dual: assigned comm evaluator
        return new BidOpeningDto(rfq.Id, rfq.Code, rfq.Title, rfq.Envelope.ToString(), rfq.Status.ToString(),
            rfq.TechnicalOpened, rfq.CommercialOpened, rfq.TechFinalized, invited, submitted,
            rfq.TechnicalEvaluatorIds, canTech, canComm);
    }

    private async Task<TechnicalEvalDto> EvalDto(Rfq rfq, CancellationToken ct)
    {
        var masked = Masked();
        var vendorIds = await SubmittedVendorIds(rfq.Id, ct);
        var scores = await db.TechnicalScores.AsNoTracking().Where(s => s.RfqId == rfq.Id).ToListAsync(ct);
        var vendors = await db.Vendors.AsNoTracking().Where(v => vendorIds.Contains(v.Id)).ToListAsync(ct);
        var users = await db.Users.AsNoTracking().ToListAsync(ct);
        var bids = await db.Bids.AsNoTracking().Include(b => b.Answers)
            .Where(b => b.RfqId == rfq.Id && b.Submitted).ToListAsync(ct);

        var evaluators = rfq.TechnicalEvaluatorIds
            .Select(code => new EvaluatorDto(code, users.FirstOrDefault(u => u.Code == code)?.Name ?? code))
            .ToList();

        // Technical questionnaire items → read-only responses table beneath the matrix.
        var techQuestions = rfq.FormItems
            .Where(i => i.Group == "technical" && i.Kind == "question")
            .OrderBy(i => i.Order)
            .Select(i => new QaItemDto(i.Order, i.Label, i.Type, i.ConfigJson)).ToList();
        var techOrders = techQuestions.Select(q => q.Order).ToHashSet();

        var vendorDtos = vendorIds.Select(vid =>
        {
            var committee = TechnicalEvaluation.Committee(scores, vid, rfq.TechnicalEvaluatorIds);
            var cells = scores.Where(s => s.VendorId == vid)
                .Select(s => new ScoreCellDto(s.EvaluatorId, s.Criterion, s.Score)).ToList();
            var display = masked
                ? TechnicalEvaluation.Alias(SourcingMapping.LiveInvitedVendorIds(rfq), vid)
                : vendors.FirstOrDefault(v => v.Id == vid)?.Name ?? "Vendor";
            var answers = (bids.FirstOrDefault(b => b.VendorId == vid)?.Answers ?? [])
                .Where(a => techOrders.Contains(a.QuestionOrder))
                .Select(a => new QaAnswerDto(a.QuestionOrder, a.Value)).ToList();
            return new VendorScoreDto(vid, display, cells, committee,
                rfq.TechFinalized ? TechnicalEvaluation.Pass(committee) : null, answers);
        }).ToList();

        return new TechnicalEvalDto(rfq.Id, rfq.Code, rfq.Title, rfq.TechFinalized, TechnicalCriteria.Threshold, masked,
            TechnicalCriteria.All.Select(c => new CriterionDto(c.Key, c.Label, c.Weight)).ToList(),
            evaluators, vendorDtos, techQuestions);
    }
}
