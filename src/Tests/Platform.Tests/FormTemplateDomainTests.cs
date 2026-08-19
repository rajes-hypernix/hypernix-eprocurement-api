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
    public void Rename_and_ReplaceQuestions_round_trip()
    {
        var template = FormTemplate.Create("rfq-q", "Old name");
        template.AddQuestion(0, "A", "short_text", true);
        template.Rename("New questionnaire");
        template.ReplaceQuestions(
        [
            (0, "ISO cert?", "yesno", true, null, "help"),
            (1, "Lead time", "number", false, """{"unit":"weeks"}""", null),
        ]);

        template.Name.ShouldBe("New questionnaire");
        template.Questions.Count.ShouldBe(2);
        template.Questions[0].Label.ShouldBe("ISO cert?");
        template.Questions[0].Type.ShouldBe("yesno");
        template.Questions[1].ConfigJson.ShouldNotBeNull();
        template.Questions[1].ConfigJson!.ShouldContain("weeks");
    }

    [Fact]
    public void SetActive_toggles_flag()
    {
        var template = FormTemplate.Create("t1", "T");
        template.IsActive.ShouldBeTrue();
        template.SetActive(false);
        template.IsActive.ShouldBeFalse();
        template.SetActive(true);
        template.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void OrgUnit_normalizes_code()
    {
        var unit = OrgUnit.Create("ops", "Operations", OrgUnitType.Department);
        unit.Code.ShouldBe("OPS");
        unit.Type.ShouldBe(OrgUnitType.Department);
    }
}

public sealed class IncotermDomainTests
{
    [Fact]
    public void Create_normalizes_code_and_is_active()
    {
        var term = Incoterm.Create(" fob ", "Free On Board");
        term.Code.ShouldBe("FOB");
        term.Name.ShouldBe("Free On Board");
        term.IsActive.ShouldBeTrue();
    }

    [Fact]
    public void SetActive_and_Update_name()
    {
        var term = Incoterm.Create("DAP", "Delivered at Place");
        term.SetActive(false);
        term.IsActive.ShouldBeFalse();
        term.Update("Delivered At Place (rev)");
        term.Name.ShouldBe("Delivered At Place (rev)");
        term.Code.ShouldBe("DAP");
    }

    [Fact]
    public void Compose_and_Parse_round_trip()
    {
        Incoterm.Compose("FOB", "Port Klang").ShouldBe("FOB Port Klang");
        Incoterm.Compose("CIF", null).ShouldBe("CIF");
        var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "FOB", "CIF" };
        Incoterm.Parse("FOB Port Klang", known).ShouldBe(("FOB", "Port Klang"));
        Incoterm.Parse("unknown place", known).ShouldBe((null, "unknown place"));
    }
}
