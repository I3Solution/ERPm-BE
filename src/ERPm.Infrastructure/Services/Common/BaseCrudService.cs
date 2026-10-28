using System.Linq.Expressions;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Domain.Common;
using ERPm.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>
/// Generic Base CRUD Service cài đặt sẵn toàn bộ logic EF Core, Paging, Soft-delete và Mapster.
/// Cung cấp các Extension Hooks ảo (Virtual Hooks) cho phép mở rộng nghiệp vụ tùy ý mà không cần viết lại CRUD.
/// </summary>
public class BaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, TKey>
    : IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, TKey>
    where TEntity : class
{
    protected readonly ApplicationDbContext Context;
    protected readonly DbSet<TEntity> DbSet;

    public BaseCrudService(ApplicationDbContext context)
    {
        Context = context;
        DbSet = context.Set<TEntity>();
    }

    public virtual async Task<PagedResult<TDto>> GetPagedAsync(BaseFilterQuery query, CancellationToken cancellationToken = default)
    {
        var queryable = DbSet.AsNoTracking();

        // 1. Tự động lọc Soft-Delete nếu thực thể kế thừa ISoftDelete
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            queryable = queryable.Where(e => !((ISoftDelete)e).IsDeleted);
        }

        // 2. Áp dụng bộ lọc tùy chỉnh
        queryable = ApplyFilters(queryable, query);

        var totalCount = await queryable.CountAsync(cancellationToken);

        // 3. Sắp xếp
        queryable = ApplySorting(queryable, query);

        // 4. Phân trang
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = Math.Max(1, query.PageSize);

        var items = await queryable
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var dtos = items.Select(MapToDto).ToList();

        return new PagedResult<TDto>
        {
            Items = dtos,
            TotalCount = totalCount,
            PageIndex = pageIndex,
            PageSize = pageSize
        };
    }

    public virtual async Task<TDto?> GetByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        if (entity == null) return default;

        return MapToDto(entity);
    }

    public virtual async Task<TDto> CreateAsync(TCreateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = MapToEntity(dto);

        // Gán thời gian tạo nếu là BaseAuditableEntity
        if (entity is BaseAuditableEntity auditable)
        {
            auditable.CreatedAtUtc = DateTime.UtcNow;
            if (auditable.Id == Guid.Empty)
            {
                auditable.Id = Guid.NewGuid();
            }
        }

        await BeforeCreateAsync(entity, dto, cancellationToken);

        await DbSet.AddAsync(entity, cancellationToken);
        await Context.SaveChangesAsync(cancellationToken);

        await AfterCreateAsync(entity, dto, cancellationToken);

        return MapToDto(entity);
    }

    public virtual async Task<TDto?> UpdateAsync(TKey id, TUpdateDto dto, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        if (entity == null) return default;

        await BeforeUpdateAsync(entity, dto, cancellationToken);

        MapUpdateToEntity(dto, entity);

        if (entity is BaseAuditableEntity auditable)
        {
            auditable.LastModifiedAtUtc = DateTime.UtcNow;
        }

        await Context.SaveChangesAsync(cancellationToken);

        await AfterUpdateAsync(entity, dto, cancellationToken);

        return MapToDto(entity);
    }

    public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken cancellationToken = default)
    {
        var entity = await FindEntityAsync(id, cancellationToken);
        if (entity == null) return false;

        await BeforeDeleteAsync(entity, cancellationToken);

        if (entity is ISoftDelete softDelete)
        {
            softDelete.IsDeleted = true;
            softDelete.DeletedAtUtc = DateTime.UtcNow;
        }
        else
        {
            DbSet.Remove(entity);
        }

        await Context.SaveChangesAsync(cancellationToken);

        await AfterDeleteAsync(entity, cancellationToken);

        return true;
    }

    #region Virtual Hooks & Mapping Methods

    /// <summary>
    /// Tìm entity theo khóa chính, tự động bỏ qua nếu đã bị soft-delete
    /// </summary>
    protected virtual async Task<TEntity?> FindEntityAsync(TKey id, CancellationToken cancellationToken)
    {
        var entity = await DbSet.FindAsync(new object?[] { id }, cancellationToken);
        if (entity != null && entity is ISoftDelete softDelete && softDelete.IsDeleted)
        {
            return null;
        }
        return entity;
    }

    /// <summary>
    /// Lọc nâng cao theo Keyword hoặc IsActive (Các lớp con có thể override để thêm điều kiện tìm kiếm)
    /// </summary>
    protected virtual IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> query, BaseFilterQuery filter)
    {
        if (filter.IsActive.HasValue)
        {
            var prop = typeof(TEntity).GetProperty("IsActive");
            if (prop != null && prop.PropertyType == typeof(bool))
            {
                var parameter = Expression.Parameter(typeof(TEntity), "e");
                var propertyAccess = Expression.Property(parameter, prop);
                var constant = Expression.Constant(filter.IsActive.Value);
                var equality = Expression.Equal(propertyAccess, constant);
                var lambda = Expression.Lambda<Func<TEntity, bool>>(equality, parameter);
                query = query.Where(lambda);
            }
        }

        return query;
    }

    /// <summary>
    /// Sắp xếp mặc định hoặc theo trường người dùng yêu cầu
    /// </summary>
    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, BaseFilterQuery filter)
    {
        // Mặc định sắp xếp theo CreatedAtUtc giảm dần nếu có
        var createdAtProp = typeof(TEntity).GetProperty("CreatedAtUtc");
        if (createdAtProp != null && string.IsNullOrWhiteSpace(filter.SortBy))
        {
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var propertyAccess = Expression.Property(parameter, createdAtProp);
            var lambda = Expression.Lambda(propertyAccess, parameter);
            var methodName = "OrderByDescending";
            var resultExp = Expression.Call(typeof(Queryable), methodName, new Type[] { typeof(TEntity), createdAtProp.PropertyType }, query.Expression, Expression.Quote(lambda));
            return query.Provider.CreateQuery<TEntity>(resultExp);
        }

        return query;
    }

    /// <summary>
    /// Chuyển Entity sang DTO (Mặc định dùng Mapster)
    /// </summary>
    protected virtual TDto MapToDto(TEntity entity)
    {
        return entity.Adapt<TDto>()!;
    }

    /// <summary>
    /// Chuyển CreateDTO sang Entity mới (Mặc định dùng Mapster)
    /// </summary>
    protected virtual TEntity MapToEntity(TCreateDto dto)
    {
        return dto.Adapt<TEntity>()!;
    }

    /// <summary>
    /// Áp dụng dữ liệu từ UpdateDTO vào Entity đang tồn tại
    /// </summary>
    protected virtual void MapUpdateToEntity(TUpdateDto dto, TEntity entity)
    {
        dto.Adapt(entity);
    }

    // Các hooks mở rộng nghiệp vụ trước và sau khi lưu DB
    protected virtual Task BeforeCreateAsync(TEntity entity, TCreateDto dto, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task AfterCreateAsync(TEntity entity, TCreateDto dto, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task BeforeUpdateAsync(TEntity entity, TUpdateDto dto, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task AfterUpdateAsync(TEntity entity, TUpdateDto dto, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task AfterDeleteAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    #endregion
}

/// <summary>
/// Shorthand Base CRUD Service với Id là Guid
/// </summary>
public class BaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto>
    : BaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, Guid>,
      IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto>
    where TEntity : class
{
    public BaseCrudService(ApplicationDbContext context) : base(context)
    {
    }
}
