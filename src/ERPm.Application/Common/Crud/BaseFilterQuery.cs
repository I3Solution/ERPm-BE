namespace ERPm.Application.Common.Crud;

/// <summary>
/// Standard pagination, sorting, and filtering query for ERP list endpoints.
/// </summary>
public class BaseFilterQuery
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public string? Keyword { get; set; }
    public string? SortBy { get; set; }
    public bool IsAscending { get; set; } = true;

    /// <summary>Short name of <see cref="Keyword"/> (<c>q</c>); the first one given wins.</summary>
    public string? Q { get => Keyword; set => Keyword ??= value; }

    /// <summary>Sort as <c>field</c> (ascending) or <c>-field</c> (descending); sets <see cref="SortBy"/> and <see cref="IsAscending"/>.</summary>
    public string? Sort
    {
        get => SortBy == null ? null : (IsAscending ? SortBy : "-" + SortBy);
        set { if (!string.IsNullOrWhiteSpace(value)) { IsAscending = !value.StartsWith('-'); SortBy = value.TrimStart('-'); } }
    }

    /// <summary>Only documents dated on or after this instant (UTC); ignored by services without a document date.</summary>
    public DateTime? From { get; set; }

    /// <summary>Only documents dated before the end of this day (UTC); ignored by services without a document date.</summary>
    public DateTime? To { get; set; }

    /// <summary>
    /// Asks a list for rows without their lines. Documents whose list rows already carry no lines ignore it; orders skip
    /// loading the lines, which is what makes their list heavy.
    /// </summary>
    public bool Summary { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>Document status; only services of documents with a status honour it.</summary>
    public string? Status { get; set; }

    /// <summary>Employee in charge; only services of documents with a salesperson honour it.</summary>
    public int? SalespersonId { get; set; }
}
