using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.HumanResource;
using ERPm.Domain.Entities.HumanResource;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.HRM;

/// <summary>
/// Manages HRM data-permission rows of accounts (HRM Module, Admin only).
/// </summary>
[Authorize(Policy = "Admin")]
[Route("api/v1/hrm/data-permissions")]
public class DataPermissionsController : BaseCrudController<HrmDataPermission, HrmDataPermissionDto, CreateHrmDataPermissionRequest, UpdateHrmDataPermissionRequest>
{
    public DataPermissionsController(IBaseCrudService<HrmDataPermission, HrmDataPermissionDto, CreateHrmDataPermissionRequest, UpdateHrmDataPermissionRequest> service)
        : base(service)
    {
    }
}
