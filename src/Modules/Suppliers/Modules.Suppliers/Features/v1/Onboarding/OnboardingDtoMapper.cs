using FSH.Modules.Suppliers.Contracts.Dtos;
using FSH.Modules.Suppliers.Domain;
using FSH.Modules.Suppliers.Domain.Onboarding;

namespace FSH.Modules.Suppliers.Features.v1.Onboarding;

internal static class OnboardingDtoMapper
{
    internal static OnboardingInvitationDto ToDto(VendorOnboardingInvitation invitation, string? applicationCode, string? magicLink)
    {
        ArgumentNullException.ThrowIfNull(invitation);
        return new OnboardingInvitationDto(
            invitation.Id,
            invitation.Email,
            invitation.Type,
            invitation.Status.ToString(),
            invitation.InvitedByName,
            invitation.CreatedUtc,
            invitation.ExpiresUtc,
            invitation.ApplicationId,
            applicationCode,
            magicLink,
            invitation.SelectedTemplateIds);
    }

    internal static OnboardingApplicationDto ToDto(VendorOnboardingApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return new OnboardingApplicationDto(
            application.Id,
            application.Code,
            application.Status.ToString(),
            application.Type,
            application.Name,
            application.Email,
            application.CreatedUtc,
            application.SubmittedUtc,
            application.SelectedTemplateIds,
            [.. application.Rounds.OrderBy(r => r.RoundNo).Select(ToRoundDto)]);
    }

    internal static OnboardingQueueItemDto ToQueueItemDto(VendorOnboardingApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        var openRound = application.Rounds.LastOrDefault(r => r.Status == ClarificationRoundStatus.Open);
        return new OnboardingQueueItemDto(
            application.Id,
            application.Code,
            application.Name,
            application.Type,
            application.Status.ToString(),
            application.Source.ToString(),
            application.CreatedUtc,
            application.SubmittedUtc,
            openRound?.RoundNo,
            application.Rounds.Count,
            application.InvitationId);
    }

    internal static OnboardingDraftDto ToDraftDto(VendorOnboardingApplication application)
    {
        ArgumentNullException.ThrowIfNull(application);
        return new OnboardingDraftDto(
            application.Id,
            application.Code,
            application.Status.ToString(),
            application.Type,
            application.Name,
            application.RegisteredName,
            application.RegistrationNo,
            application.TaxId,
            application.Email,
            application.ContactName,
            application.ContactPhone,
            application.Region,
            application.State,
            application.City,
            application.Country,
            application.Categories,
            ToContactDtos(application.Contacts),
            ToAddressDtos(application.Addresses),
            ToBankAccountDtos(application.BankAccounts),
            ToCertificationDtos(application.Certifications),
            ToFinancialYearDtos(application.Financial),
            ToDocumentDtos(application.Documents),
            application.SelectedTemplateIds,
            [.. application.Answers.Select(a => new OnboardingAnswerDto(a.FormTemplateId, a.QuestionOrder, a.Value))]);
    }

    internal static OnboardingReviewDto ToReviewDto(VendorOnboardingApplication application, string? duplicateWarning)
    {
        ArgumentNullException.ThrowIfNull(application);
        return new OnboardingReviewDto(
            application.Id,
            application.Code,
            application.Status.ToString(),
            application.Type,
            application.Source.ToString(),
            application.Name,
            application.RegisteredName,
            application.RegistrationNo,
            application.TaxId,
            application.Email,
            application.ContactName,
            application.ContactPhone,
            application.Region,
            application.State,
            application.City,
            application.Country,
            application.Categories,
            ToContactDtos(application.Contacts),
            ToAddressDtos(application.Addresses),
            ToBankAccountDtos(application.BankAccounts),
            ToCertificationDtos(application.Certifications),
            ToFinancialViewDto(application.Financial),
            ToDocumentDtos(application.Documents),
            [.. application.Rounds.OrderBy(r => r.RoundNo).Select(ToRoundDto)],
            [.. application.Answers.Select(a => new OnboardingAnswerDto(a.FormTemplateId, a.QuestionOrder, a.Value))],
            duplicateWarning,
            application.CreatedUtc,
            application.SubmittedUtc,
            application.DecisionUtc);
    }

    internal static OnboardingRoundDto ToRoundDto(OnboardingClarificationRound round)
    {
        ArgumentNullException.ThrowIfNull(round);
        return new OnboardingRoundDto(
            round.RoundNo,
            round.Direction.ToString(),
            round.Status.ToString(),
            round.Message,
            round.RaisedByName,
            round.RaisedUtc,
            round.RespondedUtc,
            [.. round.Items.Select(i => new OnboardingRoundItemDto(i.Topic, i.Request, i.Response))]);
    }

    internal static OnboardingFinancialViewDto? ToFinancialViewDto(VendorFinancialAssessment? financial)
    {
        if (financial is null || financial.Years.Count == 0)
        {
            return null;
        }

        double z = financial.LiveWeightedZ;
        var band = AltmanZModel.BandFor(z);
        var years = financial.Years
            .OrderBy(y => y.YearIndex)
            .Select(y =>
            {
                var c = AltmanZModel.Components(y);
                return new OnboardingFinancialYearCalcDto(y.YearIndex, c.X1, c.X2, c.X3, c.X4, c.X5, AltmanZModel.Z(y));
            })
            .ToList();

        return new OnboardingFinancialViewDto(
            band.ToString(),
            AltmanZModel.RiskFor(band).ToString(),
            ZoneFor(band),
            z,
            AltmanZModel.ScoreFor(z),
            AltmanZModel.StatementFor(band),
            years);
    }

    private static string ZoneFor(FinancialBand band) => band switch
    {
        FinancialBand.A => "Safe zone",
        FinancialBand.B => "Safe / grey",
        FinancialBand.C => "Grey zone",
        _ => "Distress zone",
    };

    private static IReadOnlyList<OnboardingFinancialYearDto> ToFinancialYearDtos(VendorFinancialAssessment? financial) =>
        financial is null
            ? []
            : [.. financial.Years.OrderBy(y => y.YearIndex).Select(y => new OnboardingFinancialYearDto(
                y.YearIndex, y.Revenue, y.NetProfit, y.Ebit, y.TotalAssets, y.CurrentAssets, y.Inventory,
                y.CurrentLiabilities, y.TotalLiabilities, y.Equity, y.RetainedEarnings, y.FixedAssets))];

    private static IReadOnlyList<OnboardingDocumentDto> ToDocumentDtos(IReadOnlyList<OnboardingDocument> documents) =>
        [.. documents.Select(d => new OnboardingDocumentDto(d.Key, d.FileName, d.UploadedUtc))];

    internal static IReadOnlyList<VendorContactDto> ToContactDtos(IReadOnlyList<VendorContact> contacts) =>
        [.. contacts.Select(c => new VendorContactDto(c.Name, c.Role, c.Email, c.Phone, c.IsPrimary))];

    internal static IReadOnlyList<VendorAddressDto> ToAddressDtos(IReadOnlyList<VendorAddress> addresses) =>
        [.. addresses.Select(a => new VendorAddressDto(a.Type, a.Line, a.City, a.State, a.Country, a.Postcode, a.IsPrimary))];

    internal static IReadOnlyList<VendorBankAccountDto> ToBankAccountDtos(IReadOnlyList<VendorBankAccount> bankAccounts) =>
        [.. bankAccounts.Select(b => new VendorBankAccountDto(b.Bank, b.AccountNo, b.Swift, b.Currency, b.IsPrimary))];

    internal static IReadOnlyList<VendorCertificationDto> ToCertificationDtos(IReadOnlyList<VendorCertification> certifications) =>
        [.. certifications.Select(c => new VendorCertificationDto(c.Name, c.Number, c.ValidTo, c.Status))];
}
