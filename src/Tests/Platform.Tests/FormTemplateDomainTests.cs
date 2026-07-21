using FSH.Modules.Platform.Contracts;
using FSH.Modules.Platform.Domain;
using Shouldly;
using Xunit;

namespace Platform.Tests;

public sealed class FormTemplateDomainTests
{
    [Fact]
    public void AddQuestion_rejects_duplicate_order()
    {
        var template = FormTemplate.Create(FormTemplateKeys.OnboardingSwec, "SWEC");
        template.AddQuestion(1, "Q1", "short_text", true);
        Should.Throw<PlatformRuleException>(() => template.AddQuestion(1, "Dup", "short_text", false));
    }

    [Fact]
    public void OrgUnit_normalizes_code()
    {
        var unit = OrgUnit.Create("ops", "Operations", OrgUnitType.Department);
        unit.Code.ShouldBe("OPS");
        unit.Type.ShouldBe(OrgUnitType.Department);
    }
}
