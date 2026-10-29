using ERPm.API.Controllers.Base;
using ERPm.Application.Common;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Application.Features.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Manages employee profiles (HRM Module). Personal data: Admin only until data-scope filtered reads are added.
/// </summary>
[Authorize(Policy = "Admin")]
[Route("api/v1/hrm/employees")]
public class EmployeesController : BaseCrudController<Employee, EmployeeDto, CreateEmployeeRequest, UpdateEmployeeRequest>
{
    private readonly IHrmDataScopeService _scope;

    public EmployeesController(IBaseCrudService<Employee, EmployeeDto, CreateEmployeeRequest, UpdateEmployeeRequest> service, IHrmDataScopeService scope)
        : base(service)
    {
        _scope = scope;
    }

    /// <summary>
    /// Gets the approved work history of an employee and its employment segments by legal entity for a period.
    /// </summary>
    [HttpGet("{id:int}/timeline")]
    public async Task<ActionResult<ApiResponse<EmployeeTimelineDto>>> GetTimeline(int id, [FromQuery] DateOnly from, [FromQuery] DateOnly to, CancellationToken cancellationToken)
    {
        if (await Service.GetByIdAsync(id, cancellationToken) == null) return NotFound(ApiResponse<EmployeeTimelineDto>.Fail("error.validation.reference"));
        if (from == default || to < from) return BadRequest(ApiResponse<EmployeeTimelineDto>.Fail("error.validation.request"));
        var timeline = await _scope.GetTimelineAsync(id, cancellationToken);
        return Ok(ApiResponse<EmployeeTimelineDto>.Ok(new EmployeeTimelineDto
        {
            EmployeeId = id,
            Entries = timeline.Entries.ToList(),
            EmploymentSegments = timeline.EmploymentSegments(from, to).ToList()
        }));
    }
}
