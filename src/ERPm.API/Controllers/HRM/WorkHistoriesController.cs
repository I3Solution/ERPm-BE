using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Manages work-history (QTCT) rows (HRM Module). Admin only until data-scope filtered access is added.
/// </summary>
[Authorize(Policy = "Admin")]
[Route("api/v1/hrm/work-histories")]
public class WorkHistoriesController : BaseCrudController<WorkHistory, WorkHistoryDto, CreateWorkHistoryRequest, UpdateWorkHistoryRequest>
{
    public WorkHistoriesController(IBaseCrudService<WorkHistory, WorkHistoryDto, CreateWorkHistoryRequest, UpdateWorkHistoryRequest> service)
        : base(service)
    {
    }
}
