using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Services;
using Mediator;

namespace FSH.Modules.Platform.Features.v1.Numbering.PeekDocumentNumber;

public sealed class PeekDocumentNumberQueryHandler(IDocumentNumberGenerator generator)
    : IQueryHandler<PeekDocumentNumberQuery, string>
{
    public async ValueTask<string> Handle(PeekDocumentNumberQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await generator.PeekAsync(query.RecordType, cancellationToken).ConfigureAwait(false);
    }
}
