using ERPm.Domain.Common;
using ERPm.Domain.Entities.Catalog;
using ERPm.Domain.Entities.HumanResource;
using ERPm.Domain.Entities.System;
using PartnerEntity = ERPm.Domain.Entities.Partner.Partner;

namespace ERPm.Domain.Entities.Sales;

public static class SalesOrderStatus
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";   // Waiting for a credit limit approval
    public const string Confirmed = "Confirmed";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";
}

public static class CreditCheckStatus
{
    public const string Passed = "Passed";
    public const string Warning = "Warning";                   // Within the limit but close to it
    public const string PendingApproval = "PendingApproval";   // Over the limit, needs sd:order:approve_credit
    public const string Overridden = "Overridden";             // Over the limit and approved
}

public static class OrderReservationStatus
{
    public const string None = "None";
    public const string Reserved = "Reserved";
    public const string Released = "Released";
}

public class SalesOrder : BaseAuditableEntity
{
    public string OrderNumber { get; set; } = string.Empty; // e.g. SO0001
    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    public int BranchId { get; set; }                      // Ordering branch ID
    public int? DepartmentId { get; set; }                 // Owning department, used by DEPARTMENT data scopes
    public int? CustomerId { get; set; }
    public string CustomerName { get; set; } = "Khách lẻ";

    // Monetary fields in decimal(18, 4)
    public decimal SubTotal { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal TaxAmount { get; set; } = 0m;
    public decimal TotalAmount { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;

    public string Status { get; set; } = SalesOrderStatus.Draft;
    public string CreditCheckStatus { get; set; } = Sales.CreditCheckStatus.Passed;
    public string ReservationStatus { get; set; } = OrderReservationStatus.None;
    public string? CreditApprovalReason { get; set; }
    public string? CancelReason { get; set; }
    public string? Notes { get; set; }

    public DateTime? ExpectedDeliveryDate { get; set; }
    public string? DeliveryAddress { get; set; }
    public int? QuotationId { get; set; }                  // Quotation this order was converted from
    public int? SalespersonId { get; set; }                // Employee in charge
    public int? PriceListId { get; set; }

    // Navigation
    public virtual Branch? Branch { get; set; }
    public virtual PartnerEntity? Customer { get; set; }
    public virtual SalesQuotation? Quotation { get; set; }
    public virtual Employee? Salesperson { get; set; }
    public virtual ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}

public class SalesOrderItem : BaseEntity
{
    public int SalesOrderId { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }

    public decimal Quantity { get; set; } = 0m;
    public decimal UnitPrice { get; set; } = 0m;
    public decimal DiscountAmount { get; set; } = 0m;
    public decimal LineTotal { get; set; } = 0m;
    public decimal InvoicedQuantity { get; set; } = 0m;    // Quantity already invoiced, decimal(18, 4)

    // Navigation
    public virtual SalesOrder? SalesOrder { get; set; }
    public virtual Item? Item { get; set; }
}
