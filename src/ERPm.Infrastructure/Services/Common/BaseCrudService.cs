using System.Linq.Expressions;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Domain.Common;
using ERPm.Infrastructure.Persistence;
using Mapster;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services.Common;

/// <summary>
/// Generic Base CRUD Service implementing EF Core persistence, paging, soft-delete, and Mapster mapping.
/// Provides virtual extension hooks for custom domain business logic without rewriting CRUD boilerplate.
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
        var queryable = PrepareQuery(DbSet.AsNoTracking());

        // 1. Automatically filter soft-deleted entities if TEntity implements ISoftDelete
        if (typeof(ISoftDelete).IsAssignableFrom(typeof(TEntity)))
        {
            queryable = queryable.Where(e => !((ISoftDelete)e).IsDeleted);
        }

        // 2. Apply custom filters
        queryable = ApplyFilters(queryable, query);

        var totalCount = await queryable.CountAsync(cancellationToken);

        // 3. Apply sorting
        queryable = ApplySorting(queryable, query);

        // 4. Apply pagination
        var pageIndex = Math.Clamp(query.PageIndex, 1, 1000000);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);

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

        // Populate audit timestamp and time-ordered UUIDv7 ExternalId while letting DB generate int Id
        if (entity is BaseEntity baseEntity && baseEntity.ExternalId == Guid.Empty)
        {
            baseEntity.ExternalId = Uuid7Generator.NewUuid7();
        }

        if (entity is BaseAuditableEntity auditable)
        {
            auditable.CreatedAtUtc = DateTime.UtcNow;
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
    protected virtual IQueryable<TEntity> PrepareQuery(IQueryable<TEntity> query) => query;

    /// <summary>
    /// Finds an entity by primary key and ignores it if soft-deleted.
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
    /// Applies keyword or active status filters (subclasses can override to add custom filter predicates).
    /// </summary>
    protected virtual IQueryable<TEntity> ApplyFilters(IQueryable<TEntity> query, BaseFilterQuery filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var parameter = Expression.Parameter(typeof(TEntity), "e");
            var keyword = Expression.Constant(filter.Keyword.Trim().ToLowerInvariant());
            Expression? predicate = null;
            foreach (var name in new[] { "Code", "Name", "OrderNumber", "CustomerName", "SupplierName" })
            {
                var property = typeof(TEntity).GetProperty(name);
                if (property?.PropertyType != typeof(string)) continue;
                var value = Expression.Property(parameter, property);
                var contains = Expression.AndAlso(Expression.NotEqual(value, Expression.Constant(null, typeof(string))),
                    Expression.Call(Expression.Call(value, nameof(string.ToLower), Type.EmptyTypes), nameof(string.Contains), Type.EmptyTypes, keyword));
                predicate = predicate == null ? contains : Expression.OrElse(predicate, contains);
            }
            if (predicate != null) query = query.Where(Expression.Lambda<Func<TEntity, bool>>(predicate, parameter));
        }
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
    /// Applies default sorting or user-requested sort column.
    /// </summary>
    protected virtual IQueryable<TEntity> ApplySorting(IQueryable<TEntity> query, BaseFilterQuery filter)
    {
        if (!string.IsNullOrWhiteSpace(filter.SortBy))
        {
            var property = typeof(TEntity).GetProperty(filter.SortBy);
            if (property == null || !(property.PropertyType.IsValueType || property.PropertyType == typeof(string)))
                throw new ERPm.Application.Common.BusinessException("error.validation.request");
            var argument = Expression.Parameter(typeof(TEntity), "e");
            var selector = Expression.Lambda(Expression.Property(argument, property), argument);
            var call = Expression.Call(typeof(Queryable), filter.IsAscending ? "OrderBy" : "OrderByDescending", [typeof(TEntity), property.PropertyType], query.Expression, Expression.Quote(selector));
            return query.Provider.CreateQuery<TEntity>(call);
        }
        // Default to sorting by CreatedAtUtc descending if present
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
    /// Maps an Entity to DTO using Mapster.
    /// </summary>
    protected virtual TDto MapToDto(TEntity entity)
    {
        return entity.Adapt<TDto>()!;
    }

    /// <summary>
    /// Maps a CreateDTO to a new Entity instance using Mapster.
    /// </summary>
    protected virtual TEntity MapToEntity(TCreateDto dto)
    {
        return dto.Adapt<TEntity>()!;
    }

    /// <summary>
    /// Applies changes from UpdateDTO to an existing Entity instance.
    /// </summary>
    protected virtual void MapUpdateToEntity(TUpdateDto dto, TEntity entity)
    {
        dto.Adapt(entity);
    }

    // Business lifecycle hooks executed before and after database persistence
    protected virtual Task BeforeCreateAsync(TEntity entity, TCreateDto dto, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task AfterCreateAsync(TEntity entity, TCreateDto dto, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task BeforeUpdateAsync(TEntity entity, TUpdateDto dto, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task AfterUpdateAsync(TEntity entity, TUpdateDto dto, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task BeforeDeleteAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;
    protected virtual Task AfterDeleteAsync(TEntity entity, CancellationToken cancellationToken) => Task.CompletedTask;

    #endregion
}

/// <summary>
/// Shorthand Base CRUD Service defaulting to int primary key.
/// </summary>
public class BaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto>
    : BaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, int>,
      IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto>
    where TEntity : class
{
    public BaseCrudService(ApplicationDbContext context) : base(context)
    {
    }
}
