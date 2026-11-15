namespace ERPm.Application.Common.Crud;

/// <summary>Requested sort of a list that is not built on <see cref="BaseFilterQuery"/>: a DTO property name, ascending unless <c>-field</c> is used.</summary>
public class ListSort
{
    public string? SortBy { get; set; }
    public bool IsAscending { get; set; } = true;

    /// <summary>Sort as <c>field</c> (ascending) or <c>-field</c> (descending); sets <see cref="SortBy"/> and <see cref="IsAscending"/>.</summary>
    public string? Sort
    {
        get => SortBy == null ? null : (IsAscending ? SortBy : "-" + SortBy);
        set { if (!string.IsNullOrWhiteSpace(value)) { IsAscending = !value.StartsWith('-'); SortBy = value.TrimStart('-'); } }
    }
}
