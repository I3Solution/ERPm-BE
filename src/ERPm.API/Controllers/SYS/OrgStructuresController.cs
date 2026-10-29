using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SYS;

/// <summary>
/// Manages the organization unit tree (SYS Module).
/// </summary>
[Route("api/v1/sys/org-structures")]
public class OrgStructuresController : BaseCrudController<OrgStructure, OrgStructureDto, CreateOrgStructureRequest, UpdateOrgStructureRequest>
{
    public OrgStructuresController(IBaseCrudService<OrgStructure, OrgStructureDto, CreateOrgStructureRequest, UpdateOrgStructureRequest> service)
        : base(service)
    {
    }
}
