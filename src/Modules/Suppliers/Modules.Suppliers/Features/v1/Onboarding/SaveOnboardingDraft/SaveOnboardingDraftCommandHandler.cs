using FSH.Modules.Platform.Contracts.Services;
using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Contracts.v1.Onboarding;
using FSH.Modules.Suppliers.Data;
using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Domain.Onboarding;
using Mediator;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding.SaveOnboardingDraft;

public sealed class SaveOnboardingDraftCommandHandler(
    SuppliersDbContext dbContext,
    IFormTemplateCatalog formTemplates)
    : ICommandHandler<SaveOnboardingDraftCommand, OnboardingDraftDto>
{
    public async ValueTask<OnboardingDraftDto> Handle(SaveOnboardingDraftCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (_, application) = await OnboardingTokenGate.ResolveAsync(dbContext, command.Token, cancellationToken).ConfigureAwait(false);

        var now = DateTime.UtcNow;
        application.SaveDraft(
            command.Name, command.RegisteredName, command.RegistrationNo, command.TaxId, command.Email,
            command.ContactName, command.ContactPhone, command.Region, command.State, command.City, command.Country,
            command.Categories, now);

        if (command.Contacts is not null)
        {
            application.ReplaceContacts(command.Contacts.Select(c => new VendorContact(c.Name, c.Role, c.Email, c.Phone, c.IsPrimary)));
        }

        if (command.Addresses is not null)
        {
            application.ReplaceAddresses(command.Addresses.Select(a => new VendorAddress(a.Type, a.Line, a.City, a.State, a.Country, a.Postcode, a.IsPrimary)));
        }

        if (command.BankAccounts is not null)
        {
            application.ReplaceBankAccounts(command.BankAccounts.Select(b => new VendorBankAccount(b.Bank, b.AccountNo, b.Swift, b.Currency, b.IsPrimary)));
        }

        if (command.Certifications is not null)
        {
            application.ReplaceCertifications(command.Certifications.Select(c => new VendorCertification(c.Name, c.Number, c.ValidTo, c.Status)));
        }

        if (command.FinancialYears is not null && application.Type == "NonSwec")
        {
            OnboardingFinancialValidation.Validate(command.FinancialYears);
            var years = OnboardingFinancialValidation.ToDomain(command.FinancialYears);

            if (application.Financial is null)
            {
                application.AttachFinancial(VendorFinancialAssessment.Create(application.Id, years, now));
            }
            else
            {
                application.Financial.ReplaceYears(years);
            }
        }

        if (command.Answers is not null)
        {
            await formTemplates.ValidateAnswersAsync(
                application.SelectedTemplateIds,
                [.. command.Answers.Select(a => new FormAnswerInput(a.FormTemplateId, a.QuestionOrder, a.Value))],
                requireRequired: false,
                cancellationToken).ConfigureAwait(false);

            application.ReplaceAnswers(command.Answers.Select(a =>
                new OnboardingAnswer(a.FormTemplateId, a.QuestionOrder, a.Value)));
        }

        await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return OnboardingDtoMapper.ToDraftDto(application);
    }
}
