using ERPm.API.Controllers.Base;
using ERPm.API.Security;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Manages employee profiles; creating one also records the hire work-history row (HRM Module). Reads and writes are limited to the caller's data scope.
/// </summary>
[PermissionResource("hrm:employee")]
[Route("api/v1/hrm/employees")]
public class EmployeesController : BaseCrudController<Employee, EmployeeDto, CreateEmployeeRequest, UpdateEmployeeRequest>
{
    public EmployeesController(IBaseCrudService<Employee, EmployeeDto, CreateEmployeeRequest, UpdateEmployeeRequest> service)
        : base(service)
    {
    }
}
