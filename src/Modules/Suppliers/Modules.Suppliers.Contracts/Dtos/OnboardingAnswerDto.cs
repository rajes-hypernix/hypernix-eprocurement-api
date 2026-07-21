namespace FSH.Modules.Suppliers.Contracts.Dtos;

public sealed record OnboardingAnswerDto(Guid FormTemplateId, int QuestionOrder, string Value);
