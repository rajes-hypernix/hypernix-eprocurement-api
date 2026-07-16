using FSH.Framework.Storage.Services;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.DeleteOnboardingDocument;

public sealed class DeleteOnboardingDocumentCommandHandler(SuppliersDbContext dbContext, IStorageService storageService)
    : ICommandHandler<DeleteOnboardingDocumentCommand, Guid>
{
    public async ValueTask<Guid> Handle(DeleteOnboardingDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, command.Token, cancellationToken).ConfigureAwait(false);

        var document = application.Documents.FirstOrDefault(d => d.Key == command.Key);
        application.RemoveDocument(command.Key);
        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (document is not null)
        {
            await storageService.RemoveAsync(document.StorageKey, cancellationToken).ConfigureAwait(false);
        }

        return application.Id;
    }
}
