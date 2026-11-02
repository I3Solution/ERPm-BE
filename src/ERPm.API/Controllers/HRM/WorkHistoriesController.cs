using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Manages work-history rows, the source of truth for an employee's current position (HRM Module). Reads and writes are limited to the caller's data scope.
/// </summary>
[PermissionResource("hrm:work-history")]
[Route("api/v1/hrm/work-histories")]
public class WorkHistoriesController : BaseCrudController<WorkHistory, WorkHistoryDto, CreateWorkHistoryRequest, UpdateWorkHistoryRequest>
{
    public WorkHistoriesController(IBaseCrudService<WorkHistory, WorkHistoryDto, CreateWorkHistoryRequest, UpdateWorkHistoryRequest> service)
        : base(service)
    {
    }

    /// <summary>Hidden: replaced by <see cref="Search"/>, which binds the extended filter.</summary>
    [NonAction]
    public override Task<ActionResult<ApiResponse<PagedResult<WorkHistoryDto>>>> GetPaged(BaseFilterQuery query, CancellationToken cancellationToken) =>
        base.GetPaged(query, cancellationToken);

    /// <summary>Paged list with the standard filters plus ?employeeId={id} for one employee's timeline.</summary>
    [RequirePermission(":view")]
    [HttpGet]
    public Task<ActionResult<ApiResponse<PagedResult<WorkHistoryDto>>>> Search([FromQuery] WorkHistoryFilterQuery query, CancellationToken cancellationToken) =>
        base.GetPaged(query, cancellationToken);
}
