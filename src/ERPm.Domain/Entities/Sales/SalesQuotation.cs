using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Sales;

public static class QuotationStatus
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Converted = "Converted";
    public const string Expired = "Expired";               // Derived from ValidUntil, never stored
}

/// <summary>
/// A price offer sent to a customer. It can be converted once into a Draft <see cref="SalesOrder"/>.
/// </summary>
public class SalesQuotation : BaseAuditableEntity
{
    public string QuotationNumber { get; set; } = string.Empty;   // BG-YYYYMM-XXXX
    public DateTime QuotationDate { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow;

    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;      // Snapshot at quotation time
    public string? CustomerPhone { get; set; }
    public string? CustomerEmail { get; set; }

    public int? SalespersonId { get; set; }                       // Employee in charge
    public int BranchId { get; set; }
    public int? DepartmentId { get; set; }                        // Owning department, used by DEPARTMENT data scopes
    public int? PriceListId { get; set; }                         // List the prices were taken from, informational

    public string Status { get; set; } = QuotationStatus.Draft;   // Stored: Draft, Sent, Accepted, Rejected, Converted

    // Monetary fields in decimal(18, 4). SubTotal is gross, DiscountAmount the sum of line discounts.
    public decimal SubTotal { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public decimal TotalAmount { get; set; } = 0m;

    public string? TermsAndConditions { get; set; }
    public int? ConvertedSalesOrderId { get; set; }

    // Navigation
    public virtual PartnerEntity? Customer { get; set; }
    public virtual Employee? Salesperson { get; set; }
    public virtual Branch? Branch { get; set; }
    public virtual SalesOrder? ConvertedSalesOrder { get; set; }
    public virtual ICollection<SalesQuotationItem> Items { get; set; } = new List<SalesQuotationItem>();
}

public class SalesQuotationItem : BaseEntity
{
    public int SalesQuotationId { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal LineTotal { get; set; } = 0m;                  // Quantity * UnitPrice - DiscountAmount
    public string? Notes { get; set; }

    // Navigation
    public virtual SalesQuotation? SalesQuotation { get; set; }
    public virtual Item? Item { get; set; }
}
