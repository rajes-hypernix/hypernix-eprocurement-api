namespace FSH.Modules.Platform.Domain;

/// <summary>
/// Record types that can carry custom fields / segment tags / an entry-form layout. Deliberately
/// separate from <see cref="ViewRecordType"/> (Saved Views' own, narrower enum) — the two are
/// maintained independently, per the old source's precedent of Views having its own vocabulary.
/// </summary>
public enum PlatformRecordType { Requisition, PurchaseOrder, Vendor }

/// <summary>
/// Sparse-column value storage (see <see cref="CustomFieldValue"/>) — no JSON, ever. Image/Document
/// types are deliberately not ported (they'd need Storage integration, out of scope for this slice;
/// revisit alongside Phase 10 Documents/Files).
/// </summary>
public enum CustomFieldDataType { Text, LongText, Int, Decimal, Money, Date, DateTime, Bool, ListValue, Percent, Email, Telephone, Hyperlink, RecordRef }

/// <summary>What a RecordRef-typed field's value points at.</summary>
public enum CustomFieldRefEntity { Vendor, User, Item, Transaction }

public enum CustomFieldScope { Header, Line }

/// <summary>Inline = plain read-only text, no input control at all.</summary>
public enum CustomFieldDisplayType { Normal, Disabled, Inline }
