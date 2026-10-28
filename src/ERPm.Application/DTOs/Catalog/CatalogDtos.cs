using ERPm.Domain.Entities.Catalog;

namespace ERPm.Application.DTOs.Catalog;

public class UnitOfMeasureDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
}

public class CreateUomRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public bool IsActive { get; set; } = true;
}

public class UpdateUomRequest : CreateUomRequest
{
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public class CreateCategoryRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();
    public Guid? ParentId { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public class UpdateCategoryRequest : CreateCategoryRequest
{
}

public class ItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string> Translations { get; set; } = new();

    public ItemType ItemType { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public Guid BaseUomId { get; set; }
    public string? BaseUomName { get; set; }

    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal MinStockAlert { get; set; }
    public decimal MaxStockAlert { get; set; }

    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
    public Dictionary<string, object> Attributes { get; set; } = new();
}

public class CreateItemRequest
{
    public string Code { get; set; } = string.Empty;
    public string? Barcode { get; set; }
    public string Name { get; set; } = string.Empty;
    public Dictionary<string, string>? Translations { get; set; }

    public ItemType ItemType { get; set; } = ItemType.Goods;
    public Guid? CategoryId { get; set; }
    public Guid BaseUomId { get; set; }

    public decimal SalePrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal MinStockAlert { get; set; }
    public decimal MaxStockAlert { get; set; }

    public string? ImageUrl { get; set; }
    public string? Description { get; set; }
    public Dictionary<string, object>? Attributes { get; set; }
}

public class UpdateItemRequest : CreateItemRequest
{
    public bool IsActive { get; set; } = true;
}

public class ItemFilterQuery
{
    public string? Keyword { get; set; }
    public Guid? CategoryId { get; set; }
    public ItemType? ItemType { get; set; }
    public bool? IsActive { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

// -----------------------------------------------------------------------------
// DYNAMIC ITEM ATTRIBUTES (SCHEMA-DRIVEN METADATA)
// -----------------------------------------------------------------------------

public class ItemAttributeDto
{
    public Guid Id { get; set; }
    public Guid? CategoryId { get; set; }
    public string? CategoryName { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AttributeDataType DataType { get; set; }
    public List<string> Options { get; set; } = new();
    public string? Unit { get; set; }
    public bool IsRequired { get; set; }
    public bool IsFilterable { get; set; }
    public int SortOrder { get; set; }
}

public class CreateItemAttributeRequest
{
    public Guid? CategoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public AttributeDataType DataType { get; set; } = AttributeDataType.Text;
    public List<string> Options { get; set; } = new();
    public string? Unit { get; set; }
    public bool IsRequired { get; set; } = false;
    public bool IsFilterable { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}

public class UpdateItemAttributeRequest : CreateItemAttributeRequest
{
}

