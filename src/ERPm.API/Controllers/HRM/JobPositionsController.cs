using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Manages job positions; their default roles are assigned through /api/v1/sys/roles/job-positions (HRM Module). Reads and writes are limited to the caller's data scope.
/// </summary>
[PermissionResource("hrm:job-position")]
[Route("api/v1/hrm/job-positions")]
public class JobPositionsController : BaseCrudController<JobPosition, JobPositionDto, CreateJobPositionRequest, UpdateJobPositionRequest>
{
    public JobPositionsController(IBaseCrudService<JobPosition, JobPositionDto, CreateJobPositionRequest, UpdateJobPositionRequest> service)
        : base(service)
    {
    }
}
