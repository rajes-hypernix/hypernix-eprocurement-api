namespace eProcure.Infrastructure.Persistence;

/// <summary>
/// The one-time country conformance (Slice H T6, AN-5/AN-7), extracted from the
/// <c>ConformedVocabulary</c> migration so the SAME statements can be exercised by a test. Existing
/// free-text country LABELS ("Malaysia") are rewritten to the controlled COUNTRY Custom List CODE
/// ("MY") on both <c>Vendors.Country</c> and <c>VendorAddresses.Country</c>, so a GROUP BY stops
/// splitting one country across a label bucket and a code bucket. Rows already holding a code are
/// untouched (they don't match a label). The display label is resolved back in VendorService.
/// </summary>
public static class ConformedVocabularyBackfill
{
    // label -> code (Up).
    public static readonly string[] Up =
    [
        @"UPDATE ""Vendors"" v SET ""Country"" = clv.""Code""
          FROM ""CustomListValues"" clv JOIN ""CustomLists"" cl ON cl.""Id"" = clv.""CustomListId""
          WHERE cl.""Code"" = 'COUNTRY' AND v.""Country"" = clv.""Label"";",

        @"UPDATE ""VendorAddresses"" a SET ""Country"" = clv.""Code""
          FROM ""CustomListValues"" clv JOIN ""CustomLists"" cl ON cl.""Id"" = clv.""CustomListId""
          WHERE cl.""Code"" = 'COUNTRY' AND a.""Country"" = clv.""Label"";",
    ];

    // code -> label (Down), restoring the prior free-text values.
    public static readonly string[] Down =
    [
        @"UPDATE ""Vendors"" v SET ""Country"" = clv.""Label""
          FROM ""CustomListValues"" clv JOIN ""CustomLists"" cl ON cl.""Id"" = clv.""CustomListId""
          WHERE cl.""Code"" = 'COUNTRY' AND v.""Country"" = clv.""Code"";",

        @"UPDATE ""VendorAddresses"" a SET ""Country"" = clv.""Label""
          FROM ""CustomListValues"" clv JOIN ""CustomLists"" cl ON cl.""Id"" = clv.""CustomListId""
          WHERE cl.""Code"" = 'COUNTRY' AND a.""Country"" = clv.""Code"";",
    ];
}
