using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Department level catalog (HRM Module): Division, Department, Section, Team... Guarded by the
/// hrm:department permissions.
/// </summary>
[PermissionResource("hrm:department")]
[Route("api/v1/hrm/department-levels")]
public class DepartmentLevelsController : BaseCrudController<DepartmentLevel, DepartmentLevelDto, CreateDepartmentLevelRequest, UpdateDepartmentLevelRequest>
{
    public DepartmentLevelsController(IBaseCrudService<DepartmentLevel, DepartmentLevelDto, CreateDepartmentLevelRequest, UpdateDepartmentLevelRequest> service)
        : base(service)
    {
    }
}
