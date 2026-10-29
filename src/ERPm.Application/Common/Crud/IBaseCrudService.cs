namespace ERPm.Application.Common.Crud;

/// <summary>
/// Generic strongly-typed Base CRUD service interface.
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
/// Shorthand Base CRUD interface defaulting to int primary key.
/// </summary>
public interface IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto>
    : IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, int>
    where TEntity : class
{
}
