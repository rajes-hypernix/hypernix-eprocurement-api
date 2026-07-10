namespace eProcure.Infrastructure.Persistence;

/// <summary>
/// The one-time ownership backfill for <c>StoredFile</c> (Slice G T5), extracted from the
/// <c>StoredFileOwnership</c> migration so the SAME statements can be exercised by a test against
/// real rows. The dev seed had no files when the migration first ran, so this is the only coverage of
/// the inference: bid attachments (answer-value "&lt;fileId&gt;::name" convention) and onboarding
/// documents/answers map to the owning / promoted vendor; anything unattributable keeps the default
/// <c>OwnerKind = 'Internal'</c> (buyer/admin only — fail-closed).
///
/// Each statement claims only files still <c>'Internal'</c>, so ordering is deterministic
/// (Bid &gt; OnboardingDocument &gt; OnboardingAnswer) and re-running is idempotent.
/// </summary>
public static class StoredFileOwnershipBackfill
{
    public static readonly string[] Statements =
    [
        // 1. Bid attachments: "<fileId>::name" entries (pipe-separated) in the vendor's bid answers.
        @"UPDATE ""StoredFiles"" sf
          SET ""OwnerKind"" = 'Bid', ""OwnerVendorId"" = b.""VendorId"", ""OwnerEntityId"" = b.""Id""
          FROM ""Bids"" b
          JOIN ""BidAnswers"" ba ON ba.""BidId"" = b.""Id""
          CROSS JOIN LATERAL regexp_split_to_table(ba.""Value"", '\|') AS entry
          WHERE entry ~ '^[0-9a-fA-F-]{36}::'
            AND sf.""Id"" = substring(entry from 1 for 36)::uuid
            AND sf.""OwnerKind"" = 'Internal';",

        // 2. Onboarding documents (typed StoredFileId) -> the promoted vendor.
        @"UPDATE ""StoredFiles"" sf
          SET ""OwnerKind"" = 'OnboardingDocument', ""OwnerVendorId"" = a.""PromotedVendorId"", ""OwnerEntityId"" = a.""Id""
          FROM ""OnboardingDocuments"" od
          JOIN ""VendorOnboardingApplications"" a ON a.""Id"" = od.""VendorOnboardingApplicationId""
          WHERE sf.""Id"" = od.""StoredFileId"" AND sf.""OwnerKind"" = 'Internal';",

        // 3. Onboarding answer file refs ("<fileId>::name") -> the promoted vendor.
        @"UPDATE ""StoredFiles"" sf
          SET ""OwnerKind"" = 'OnboardingAnswer', ""OwnerVendorId"" = a.""PromotedVendorId"", ""OwnerEntityId"" = a.""Id""
          FROM ""VendorOnboardingApplications"" a
          JOIN ""OnboardingAnswers"" oa ON oa.""VendorOnboardingApplicationId"" = a.""Id""
          CROSS JOIN LATERAL regexp_split_to_table(oa.""Value"", '\|') AS entry
          WHERE entry ~ '^[0-9a-fA-F-]{36}::'
            AND sf.""Id"" = substring(entry from 1 for 36)::uuid
            AND sf.""OwnerKind"" = 'Internal';",
    ];
}
