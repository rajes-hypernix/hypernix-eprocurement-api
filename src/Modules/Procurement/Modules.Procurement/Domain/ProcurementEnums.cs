namespace FSH.Modules.Procurement.Domain;

public enum PoStatus { Draft, Issued, Acknowledged, PartiallyReceived, Received, Discrepancy, Matched }

public enum AsnStatus { InTransit, Received }

public enum InvoiceStatus { Submitted, Exception, Approved }
