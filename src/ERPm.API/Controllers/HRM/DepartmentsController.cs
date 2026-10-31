using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Manages departments and sub-units of company branches (HRM Module). Reads and writes are limited to the caller's data scope.
/// </summary>
[PermissionResource("hrm:department")]
[Route("api/v1/hrm/departments")]
public class DepartmentsController : BaseCrudController<Department, DepartmentDto, CreateDepartmentRequest, UpdateDepartmentRequest>
{
    public DepartmentsController(IBaseCrudService<Department, DepartmentDto, CreateDepartmentRequest, UpdateDepartmentRequest> service)
        : base(service)
    {
    }

    /// <summary>Hidden: replaced by <see cref="Search"/>, which binds the extended filter.</summary>
    [NonAction]
    public override Task<ActionResult<ApiResponse<PagedResult<DepartmentDto>>>> GetPaged(BaseFilterQuery query, CancellationToken cancellationToken) =>
        base.GetPaged(query, cancellationToken);

    /// <summary>Paged list with the standard filters plus ?companyId={id}, ?levelCode=SECTION and/or ?underId={id} (subtree by Path).</summary>
    [RequirePermission(":view")]
    [HttpGet]
    public Task<ActionResult<ApiResponse<PagedResult<DepartmentDto>>>> Search([FromQuery] DepartmentFilterQuery query, CancellationToken cancellationToken) =>
        base.GetPaged(query, cancellationToken);
}
