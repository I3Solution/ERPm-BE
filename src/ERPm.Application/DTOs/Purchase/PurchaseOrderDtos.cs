namespace ERPm.Application.DTOs.Purchase;

public class PurchaseOrderItemDto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
}

public class PurchaseOrderDto
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public int BranchId { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal PaidAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Notes { get; set; }

    public List<PurchaseOrderItemDto> Items { get; set; } = new();
}

public class CreatePurchaseOrderItemRequest
{
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal => Quantity * UnitPrice;
}

public class CreatePurchaseOrderRequest
{
    public string? OrderNumber { get; set; }
    public DateTime? OrderDate { get; set; }
    public int BranchId { get; set; }
    public int? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;

    public decimal TaxAmount { get; set; } = 0m;
    public decimal PaidAmount { get; set; } = 0m;
    public string? Notes { get; set; }

    public List<CreatePurchaseOrderItemRequest> Items { get; set; } = new();
}

public class UpdatePurchaseOrderRequest : CreatePurchaseOrderRequest
{
    public string? Status { get; set; }
}
