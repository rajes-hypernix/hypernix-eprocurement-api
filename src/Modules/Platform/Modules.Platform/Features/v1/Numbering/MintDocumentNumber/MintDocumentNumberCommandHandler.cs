using FSH.Modules.Platform.Contracts.v1.Configuration;
using FSH.Modules.Platform.Services;
using Mediator;

namespace FSH.Modules.Platform.Features.v1.Numbering.MintDocumentNumber;

public sealed class MintDocumentNumberCommandHandler(IDocumentNumberGenerator generator)
    : ICommandHandler<MintDocumentNumberCommand, string>
{
    public async ValueTask<string> Handle(MintDocumentNumberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        return await generator.MintAsync(command.RecordType, cancellationToken).ConfigureAwait(false);
    }
}
