using FSH.Modules.Platform.Contracts;
using FSH.Modules.Platform.Domain;
using Shouldly;
using Xunit;

namespace Platform.Tests;

public sealed class CustomListDomainTests
{
    [Fact]
    public void UpsertItem_creates_then_updates_by_code()
    {
        var list = CustomList.Create(CustomListKeys.BidDecline, "Decline reasons");

        var first = list.UpsertItem("capacity", "No capacity", 1, true);
        first.Code.ShouldBe("CAPACITY");
        list.Items.Count.ShouldBe(1);

        var second = list.UpsertItem("CAPACITY", "Still no capacity", 2, false);
        second.Id.ShouldBe(first.Id);
        second.Label.ShouldBe("Still no capacity");
        second.SortOrder.ShouldBe(2);
        second.IsActive.ShouldBeFalse();
        list.Items.Count.ShouldBe(1);
    }

    [Fact]
    public void Country_normalizes_code()
    {
        var country = Country.Create("my", "Malaysia");
        country.Code.ShouldBe("MY");
        country.IsActive.ShouldBeTrue();

        country.SetActive(false);
        country.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Bank_requires_name_and_country()
    {
        Should.Throw<ArgumentException>(() => Bank.Create(" ", "MY"));
        Should.Throw<ArgumentException>(() => Bank.Create("Maybank", " "));

        var bank = Bank.Create("Maybank", "my", "mbbemykl");
        bank.CountryCode.ShouldBe("MY");
        bank.SwiftCode.ShouldBe("MBBEMYKL");
    }
}
