using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Manages the work shift catalogue (HRM Module).
/// </summary>
[Route("api/v1/hrm/shifts")]
public class ShiftsController : BaseCrudController<Shift, ShiftDto, CreateShiftRequest, UpdateShiftRequest>
{
    public ShiftsController(IBaseCrudService<Shift, ShiftDto, CreateShiftRequest, UpdateShiftRequest> service)
        : base(service)
    {
    }
}
