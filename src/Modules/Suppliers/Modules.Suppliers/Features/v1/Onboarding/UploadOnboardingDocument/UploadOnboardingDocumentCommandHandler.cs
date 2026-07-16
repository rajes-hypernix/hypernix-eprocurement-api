using FSH.Framework.Shared.Storage;
using FSH.Framework.Storage;
using FSH.Framework.Storage.Services;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.UploadOnboardingDocument;

public sealed class UploadOnboardingDocumentCommandHandler(SuppliersDbContext dbContext, IStorageService storageService)
    : ICommandHandler<UploadOnboardingDocumentCommand, OnboardingDocumentDto>
{
    public async ValueTask<OnboardingDocumentDto> Handle(UploadOnboardingDocumentCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, command.Token, cancellationToken).ConfigureAwait(false);

        var uploadRequest = new FileUploadRequest
        {
            FileName = command.FileName,
            ContentType = command.ContentType,
            Data = [.. command.Content],
        };
        string storageKey = await storageService.UploadAsync<VendorOnboardingApplication>(uploadRequest, FileType.Document, cancellationToken)
            .ConfigureAwait(false);

        var now = DateTime.UtcNow;
        var document = new OnboardingDocument(command.Key, command.FileName, storageKey, now);
        application.AddOrReplaceDocument(document);

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new OnboardingDocumentDto(document.Key, document.FileName, document.UploadedUtc);
    }
}
