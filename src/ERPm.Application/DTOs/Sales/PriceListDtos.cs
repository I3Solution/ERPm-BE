using ERPm.Domain.Entities.Sales;

namespace ERPm.Application.DTOs.Sales;

public class PriceListItemDto
{
    public int? Id { get; set; }
    public int ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemName { get; set; } = string.Empty;
    public string? UnitName { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal DiscountPercent { get; set; }
}

public class PriceListDetailDto
{
    public int Id { get; set; }
    public Guid ExternalId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public PriceListType Type { get; set; }
    public bool IsTaxInclusive { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int? BranchId { get; set; }
    public string? BranchName { get; set; }
    public bool IsActive { get; set; }
    public string? Notes { get; set; }
    public List<PriceListItemDto> Items { get; set; } = new();
}

public class PriceListLineRequest
{
    public int ItemId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal MinQuantity { get; set; }
    public decimal DiscountPercent { get; set; }
}

public class CreatePriceListRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Currency { get; set; } = "VND";
    public PriceListType Type { get; set; } = PriceListType.Sales;
    public bool IsTaxInclusive { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public int? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Notes { get; set; }
    public List<PriceListLineRequest> Items { get; set; } = new();
}

public class UpdatePriceListRequest : CreatePriceListRequest
{
}

public class CalculatePriceLineRequest
{
    public int ItemId { get; set; }
    public decimal Quantity { get; set; }
}

public class CalculatePriceRequest
{
    public int? CustomerId { get; set; }
    public int BranchId { get; set; }
    public DateTime? DocumentDate { get; set; }
    public List<CalculatePriceLineRequest> Items { get; set; } = new();
}

public class CalculatePriceLineDto
{
    public int ItemId { get; set; }
    public int? AppliedPriceListId { get; set; }
    public string? AppliedPriceListName { get; set; }
    public int? AppliedPriceListVersionId { get; set; }
    public string? AppliedVersionCode { get; set; }
    public decimal OriginalPrice { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalWithTax { get; set; }
    public bool IsTaxInclusive { get; set; }
}

public class CalculatePriceResponse
{
    public DateTime DocumentDate { get; set; }
    public List<CalculatePriceLineDto> Items { get; set; } = new();
}

public class PriceListVersionDto
{
    public int Id { get; set; }
    public int PriceListId { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public PriceListVersionStatus Status { get; set; }
    public int ItemCount { get; set; }
    public List<PriceListItemDto> Items { get; set; } = new();
}

public class ClonePriceListVersionRequest
{
    public int SourceVersionId { get; set; }
    public string VersionCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public enum PriceAdjustmentType
{
    Percentage = 1,
    FixedAmount = 2
}

public class BulkPriceAdjustmentRequest
{
    public PriceAdjustmentType AdjustmentType { get; set; }
    public decimal Value { get; set; }
    public decimal RoundingIncrement { get; set; } = 0.01m;
}

public class BulkPriceAdjustmentResponse
{
    public int PriceListVersionId { get; set; }
    public int AdjustedLineCount { get; set; }
    public decimal RoundingIncrement { get; set; }
}
