using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.System;

namespace ERPm.Domain.Entities.Sales;

/// <summary>Which side of the business a price list serves. Stored as integer.</summary>
public enum PriceListType
{
    Sales = 1,
    Purchase = 2
}

/// <summary>Lifecycle of one price list version. Stored as integer; existing values 1..4 remain immutable.</summary>
public enum PriceListVersionStatus
{
    Draft = 1,
    Active = 2,
    Expired = 3,
    Archived = 4,
    PendingApproval = 5,
    Approved = 6
}

/// <summary>
/// A price list for a customer segment (retail, dealer, wholesale, VIP), a supplier or a season/branch.
/// The header holds the rules shared by every version; the actual prices live in <see cref="PriceListVersion"/>.
/// </summary>
public class PriceList : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;       // Unique, e.g. PL-RETAIL
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";          // ISO 4217

    public PriceListType Type { get; set; } = PriceListType.Sales;
    public bool IsTaxInclusive { get; set; }               // True when prices already contain VAT

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }             // Open-ended when null

    public int? BranchId { get; set; }                     // Null = applies to every branch
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual ICollection<PriceListVersion> Versions { get; set; } = new List<PriceListVersion>();
}

/// <summary>
/// A dated snapshot of the prices of one price list. Only versions in <see cref="PriceListVersionStatus.Active"/>
/// and inside their effective range take part in pricing.
/// </summary>
public class PriceListVersion : BaseAuditableEntity
{
    public int PriceListId { get; set; }
    public string VersionCode { get; set; } = string.Empty; // Unique inside the price list, e.g. V1, 2026-Q4

    public DateTime EffectiveFrom { get; set; } = DateTime.UtcNow;
    public DateTime? EffectiveTo { get; set; }              // Open-ended when null
    public PriceListVersionStatus Status { get; set; } = PriceListVersionStatus.Draft;

    // Workflow audit snapshot fields
    public string? SubmittedBy { get; set; }
    public DateTime? SubmittedAtUtc { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }
    public string? RejectedBy { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectReason { get; set; }
    public string? ActivatedBy { get; set; }
    public DateTime? ActivatedAtUtc { get; set; }
    public string? ClosedBy { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
    public string? CloseReason { get; set; }
    public string? ArchivedBy { get; set; }
    public DateTime? ArchivedAtUtc { get; set; }

    // Navigation
    public virtual PriceList? PriceList { get; set; }
    public virtual ICollection<PriceListItem> Items { get; set; } = new List<PriceListItem>();
    public virtual ICollection<PriceListVersionAudit> AuditLogs { get; set; } = new List<PriceListVersionAudit>();
}

/// <summary>Immutable workflow transition history entry for a price list version.</summary>
public class PriceListVersionAudit : BaseEntity
{
    public int PriceListVersionId { get; set; }
    public string Action { get; set; } = string.Empty;      // SUBMIT, WITHDRAW, APPROVE, REJECT, ACTIVATE, CLOSE, ARCHIVE
    public PriceListVersionStatus FromStatus { get; set; }
    public PriceListVersionStatus ToStatus { get; set; }
    public string? ActorUserId { get; set; }
    public string? ActorName { get; set; }
    public string? Reason { get; set; }
    public DateTime OccurredAtUtc { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual PriceListVersion? PriceListVersion { get; set; }
}

/// <summary>
/// One price line of a version. Several lines for the same item express quantity tiers (MinQuantity).
/// </summary>
public class PriceListItem : BaseEntity
{
    public int PriceListVersionId { get; set; }
    public int ItemId { get; set; }
    public int? UomId { get; set; }                        // Null = the item's base unit

    public decimal UnitPrice { get; set; } = 0m;           // decimal(18, 4)
    public decimal MinQuantity { get; set; } = 0m;         // Tier threshold, decimal(18, 4)
    public decimal DiscountPercent { get; set; } = 0m;     // 0..100, decimal(18, 4)

    // Navigation
    public virtual PriceListVersion? PriceListVersion { get; set; }
    public virtual Item? Item { get; set; }
    public virtual UnitOfMeasure? Uom { get; set; }
}
