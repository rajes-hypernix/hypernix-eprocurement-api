using FSH.Modules.Auditing.Contracts;
using FSH.Modules.Auditing.Contracts.Dtos;
using FSH.Modules.Auditing.Contracts.v1.GetEntityChangeHistory;
using FSH.Modules.Auditing.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FSH.Modules.Auditing.Features.v1.GetEntityChangeHistory;

public sealed class GetEntityChangeHistoryQueryHandler(
    AuditDbContext dbContext,
    ILogger<GetEntityChangeHistoryQueryHandler> logger)
    : IQueryHandler<GetEntityChangeHistoryQuery, IReadOnlyList<AuditDetailDto>>
{
    public const int MaxTake = 200;

    public async ValueTask<IReadOnlyList<AuditDetailDto>> Handle(
        GetEntityChangeHistoryQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var take = Math.Clamp(query.Take <= 0 ? 50 : query.Take, 1, MaxTake);
        // EntityDiffBuilder key for single-PK aggregates: "Id:{guid}".
        // Payload JSON is camelCase (SystemTextJsonAuditSerializer) → property name is "key".
        var entityId = query.EntityId.ToString();
        var entityKey = $"Id:{entityId}";
        // Prefer exact key match; also accept bare id (legacy fuzzy search behaviour).
        var keyPattern = $"%\"key\":\"{entityKey}\"%";
        var idPattern = $"%{entityId}%";

        var records = await dbContext.AuditRecords
            .AsNoTracking()
            .Where(a => a.EventType == (int)AuditEventType.EntityChange)
            .Where(a =>
                EF.Functions.ILike(AuditJsonbFunctions.AsText(a.PayloadJson), keyPattern)
                || EF.Functions.ILike(AuditJsonbFunctions.AsText(a.PayloadJson), idPattern))
            .OrderByDescending(a => a.OccurredAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var result = new List<AuditDetailDto>(records.Count);
        foreach (var record in records)
        {
            JsonElement payload;
            try
            {
                using var document = JsonDocument.Parse(record.PayloadJson);
                payload = document.RootElement.Clone();
            }
            catch (JsonException ex)
            {
                logger.LogWarning(ex, "Failed to parse audit payload JSON for record {AuditId}.", record.Id);
                payload = JsonDocument.Parse("{}").RootElement.Clone();
            }

            result.Add(new AuditDetailDto
            {
                Id = record.Id,
                OccurredAtUtc = record.OccurredAtUtc,
                ReceivedAtUtc = record.ReceivedAtUtc,
                EventType = (AuditEventType)record.EventType,
                Severity = (AuditSeverity)record.Severity,
                TenantId = record.TenantId,
                UserId = record.UserId,
                UserName = record.UserName,
                TraceId = record.TraceId,
                SpanId = record.SpanId,
                CorrelationId = record.CorrelationId,
                RequestId = record.RequestId,
                Source = record.Source,
                Tags = (AuditTag)record.Tags,
                Payload = payload,
            });
        }

        return result;
    }
}
