namespace ERPm.Application.Common.Crud;

/// <summary>
/// Interface Generic Base CRUD chuẩn, định kiểu tĩnh (Strongly-typed), type-safe 100%
/// </summary>
public interface IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, TKey>
    where TEntity : class
{
    Task<PagedResult<TDto>> GetPagedAsync(BaseFilterQuery query, CancellationToken cancellationToken = default);
    Task<TDto?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default);
    Task<TDto> CreateAsync(TCreateDto dto, CancellationToken cancellationToken = default);
    Task<TDto?> UpdateAsync(TKey id, TUpdateDto dto, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Shorthand Interface mặc định với Primary Key là Guid
/// </summary>
public interface IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto>
    : IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, Guid>
    where TEntity : class
{
}
