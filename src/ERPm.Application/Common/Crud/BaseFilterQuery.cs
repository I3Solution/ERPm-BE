namespace ERPm.Application.Common.Crud;

/// <summary>
/// Query lọc chuẩn dành cho các danh mục và màn hình danh sách ERP
/// </summary>
public class BaseFilterQuery
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsAscending { get; set; } = true;
    public bool? IsActive { get; set; }
}
