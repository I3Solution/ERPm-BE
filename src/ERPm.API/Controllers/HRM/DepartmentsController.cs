using ERPm.API.Controllers.Base;
using ERPm.API.Security;
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
}
