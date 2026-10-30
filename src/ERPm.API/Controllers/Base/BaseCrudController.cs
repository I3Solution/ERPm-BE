using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.Base;

/// <summary>
/// Generic Base CRUD Controller providing standard RESTful endpoints (GET Paged, GET by ID, POST, PUT, DELETE).
/// Each endpoint requires the IAM action ":view|:create|:edit|:delete" of the derived controller's [PermissionResource].
/// 100% Type-Safe with full OpenAPI/Swagger schema support and zero runtime reflection.
/// </summary>
public abstract class BaseCrudController<TEntity, TDto, TCreateDto, TUpdateDto, TKey> : BaseApiController
    where TEntity : class
{
    protected readonly IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, TKey> Service;

    protected BaseCrudController(IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, TKey> service)
    {
        Service = service;
    }

    /// <summary>
    /// Gets a paginated list of records supporting keyword search, active status filter, and sorting.
    /// </summary>
    [RequirePermission(":view")]
    [HttpGet]
    public virtual async Task<ActionResult<ApiResponse<PagedResult<TDto>>>> GetPaged([FromQuery] BaseFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await Service.GetPagedAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<TDto>>.Ok(result));
    }

    /// <summary>
    /// Gets details of a single record by its internal integer ID.
    /// </summary>
    [RequirePermission(":view")]
    [HttpGet("{id}")]
    public virtual async Task<ActionResult<ApiResponse<TDto>>> GetById(TKey id, CancellationToken cancellationToken)
    {
        var result = await Service.GetByIdAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<TDto>.Fail("error.validation.reference"));
        }

        return Ok(ApiResponse<TDto>.Ok(result));
    }

    /// <summary>
    /// Creates a new record.
    /// </summary>
    [RequirePermission(":create")]
    [HttpPost]
    public virtual async Task<ActionResult<ApiResponse<TDto>>> Create([FromBody] TCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await Service.CreateAsync(dto, cancellationToken);
        return Ok(ApiResponse<TDto>.Ok(result, "common.saved"));
    }

    /// <summary>
    /// Updates an existing record by its internal integer ID.
    /// </summary>
    [RequirePermission(":edit")]
    [HttpPut("{id}")]
    public virtual async Task<ActionResult<ApiResponse<TDto>>> Update(TKey id, [FromBody] TUpdateDto dto, CancellationToken cancellationToken)
    {
        var result = await Service.UpdateAsync(id, dto, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<TDto>.Fail("error.validation.reference"));
        }

        return Ok(ApiResponse<TDto>.Ok(result, "common.saved"));
    }

    /// <summary>
    /// Deletes a record by its internal integer ID (automatically soft-deletes if supported).
    /// </summary>
    [RequirePermission(":delete")]
    [HttpDelete("{id}")]
    public virtual async Task<ActionResult<ApiResponse<bool>>> Delete(TKey id, CancellationToken cancellationToken)
    {
        var success = await Service.DeleteAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.Fail("error.validation.reference"));
        }

        return Ok(ApiResponse<bool>.Ok(true, "common.saved"));
    }
}

/// <summary>
/// Shorthand Base CRUD Controller defaulting to int primary key.
/// </summary>
public abstract class BaseCrudController<TEntity, TDto, TCreateDto, TUpdateDto>
    : BaseCrudController<TEntity, TDto, TCreateDto, TUpdateDto, int>
    where TEntity : class
{
    protected BaseCrudController(IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, int> service) : base(service)
    {
    }
}
