using ERPm.API.Controllers.Base;
using ERPm.API.Security;
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
}
