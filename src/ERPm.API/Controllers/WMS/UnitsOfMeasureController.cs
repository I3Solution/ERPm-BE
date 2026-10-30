using ERPm.API.Security;
using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Catalog;
using ERPm.Domain.Entities.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.WMS;

/// <summary>
/// Manages units of measure (WMS Module).
/// Inherits BaseCrudController for full type-safe CRUD endpoints.
/// </summary>
[PermissionResource("wms:uom")]
[Route("api/v1/wms/units-of-measure")]
public class UnitsOfMeasureController : BaseCrudController<UnitOfMeasure, UnitOfMeasureDto, CreateUomRequest, UpdateUomRequest>
{
    public UnitsOfMeasureController(IBaseCrudService<UnitOfMeasure, UnitOfMeasureDto, CreateUomRequest, UpdateUomRequest> service)
        : base(service)
    {
    }
}
