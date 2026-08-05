using FSH.Modules.Suppliers.Domain;

namespace Suppliers.Tests.Domain;

public sealed class VendorParsersAndGeoTests
{
    [Theory]
    [InlineData("Swec", VendorType.Swec)]
    [InlineData("SWEC", VendorType.Swec)]
    [InlineData("NonSwec", VendorType.NonSwec)]
    [InlineData("Non-SWEC", VendorType.NonSwec)]
    [InlineData("non swec", VendorType.NonSwec)]
    [InlineData(null, VendorType.NonSwec)]
    public void VendorTypeParser_Parse_Accepts_UiAndApi_Shapes(string? input, VendorType expected)
        => VendorTypeParser.Parse(input).ShouldBe(expected);

    [Fact]
    public void VendorTypeParser_ToApi_Is_Stable()
    {
        VendorTypeParser.ToApi(VendorType.Swec).ShouldBe("Swec");
        VendorTypeParser.ToApi(VendorType.NonSwec).ShouldBe("NonSwec");
    }

    [Fact]
    public void Vendor_CreateManual_Normalizes_CountryCode_And_Rejects_Duplicate_Contact()
    {
        var vendor = Vendor.CreateManual(
            code: "V-1",
            name: "Acme",
            registeredName: null,
            registrationNo: "SSM-1",
            taxId: null,
            type: VendorType.NonSwec,
            region: "Peninsular",
            state: "Selangor",
            city: "Shah Alam",
            countryCode: "my",
            stateId: null,
            cityId: null,
            paymentTerms: "NET30",
            categories: null);

        vendor.CountryCode.ShouldBe("MY");
        vendor.Status.ShouldBe(VendorStatus.Registered);

        vendor.AddContact(new VendorContact("Ali", "Sales", "ali@example.com", null, true));
        Should.Throw<InvalidOperationException>(() =>
            vendor.AddContact(new VendorContact("Ali", "Other", "ali@example.com", null, false)));
    }

    [Fact]
    public void VendorBankAccount_Requires_BankId_And_Normalizes_Currency()
    {
        Should.Throw<ArgumentException>(() =>
            new VendorBankAccount(Guid.Empty, "Maybank", "123", null, "myr", true));

        var bankId = Guid.NewGuid();
        var account = new VendorBankAccount(bankId, " Maybank ", "123", "MBBEMYKL", "myr", true);
        account.BankId.ShouldBe(bankId);
        account.BankName.ShouldBe("Maybank");
        account.CurrencyCode.ShouldBe("MYR");
    }

    [Fact]
    public void VendorAddress_Uses_Enum_Type_And_CountryCode()
    {
        var address = new VendorAddress(
            VendorAddressType.Registered,
            "1 Jalan",
            "Shah Alam",
            "Selangor",
            "my",
            null,
            null,
            "40000",
            true);

        address.Type.ShouldBe(VendorAddressType.Registered);
        address.CountryCode.ShouldBe("MY");
    }
}
