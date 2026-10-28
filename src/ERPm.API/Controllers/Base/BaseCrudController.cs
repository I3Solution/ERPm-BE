using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.Base;

/// <summary>
/// Generic Base CRUD Controller cung cấp sẵn 5 endpoints RESTful chuẩn (GET Paged, GET by ID, POST, PUT, DELETE).
/// 100% Type-Safe, hỗ trợ OpenAPI/Swagger schema đầy đủ, không sử dụng reflection runtime.
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
    /// Lấy danh sách phân trang có hỗ trợ tìm kiếm từ khóa, lọc trạng thái và sắp xếp
    /// </summary>
    [HttpGet]
    public virtual async Task<ActionResult<ApiResponse<PagedResult<TDto>>>> GetPaged([FromQuery] BaseFilterQuery query, CancellationToken cancellationToken)
    {
        var result = await Service.GetPagedAsync(query, cancellationToken);
        return Ok(ApiResponse<PagedResult<TDto>>.Ok(result));
    }

    /// <summary>
    /// Lấy thông tin chi tiết một bản ghi theo ID
    /// </summary>
    [HttpGet("{id}")]
    public virtual async Task<ActionResult<ApiResponse<TDto>>> GetById(TKey id, CancellationToken cancellationToken)
    {
        var result = await Service.GetByIdAsync(id, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<TDto>.Fail($"Bản ghi với ID '{id}' không tồn tại."));
        }

        return Ok(ApiResponse<TDto>.Ok(result));
    }

    /// <summary>
    /// Thêm mới một bản ghi
    /// </summary>
    [HttpPost]
    public virtual async Task<ActionResult<ApiResponse<TDto>>> Create([FromBody] TCreateDto dto, CancellationToken cancellationToken)
    {
        var result = await Service.CreateAsync(dto, cancellationToken);
        return Ok(ApiResponse<TDto>.Ok(result, "Tạo mới bản ghi thành công."));
    }

    /// <summary>
    /// Cập nhật thông tin bản ghi theo ID
    /// </summary>
    [HttpPut("{id}")]
    public virtual async Task<ActionResult<ApiResponse<TDto>>> Update(TKey id, [FromBody] TUpdateDto dto, CancellationToken cancellationToken)
    {
        var result = await Service.UpdateAsync(id, dto, cancellationToken);
        if (result == null)
        {
            return NotFound(ApiResponse<TDto>.Fail($"Bản ghi với ID '{id}' không tồn tại."));
        }

        return Ok(ApiResponse<TDto>.Ok(result, "Cập nhật bản ghi thành công."));
    }

    /// <summary>
    /// Xóa bản ghi theo ID (Tự động Soft-Delete nếu thực thể có hỗ trợ)
    /// </summary>
    [HttpDelete("{id}")]
    public virtual async Task<ActionResult<ApiResponse<bool>>> Delete(TKey id, CancellationToken cancellationToken)
    {
        var success = await Service.DeleteAsync(id, cancellationToken);
        if (!success)
        {
            return NotFound(ApiResponse<bool>.Fail($"Bản ghi với ID '{id}' không tồn tại hoặc đã bị xóa."));
        }

        return Ok(ApiResponse<bool>.Ok(true, "Xóa bản ghi thành công."));
    }
}

/// <summary>
/// Shorthand Base CRUD Controller với khóa chính mặc định là Guid
/// </summary>
public abstract class BaseCrudController<TEntity, TDto, TCreateDto, TUpdateDto>
    : BaseCrudController<TEntity, TDto, TCreateDto, TUpdateDto, Guid>
    where TEntity : class
{
    protected BaseCrudController(IBaseCrudService<TEntity, TDto, TCreateDto, TUpdateDto, Guid> service) : base(service)
    {
    }
}
