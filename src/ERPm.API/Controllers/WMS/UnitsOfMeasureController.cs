using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Catalog;
using ERPm.Domain.Entities.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.WMS;

/// <summary>
/// Quản lý Đơn vị tính (WMS Module)
/// Kế thừa BaseCrudController: Tự động có trọn bộ CRUD Paged, GetById, Create, Update, Delete.
/// </summary>
[Route("api/v1/wms/units-of-measure")]
public class UnitsOfMeasureController : BaseCrudController<UnitOfMeasure, UnitOfMeasureDto, CreateUomRequest, UpdateUomRequest>
{
    public UnitsOfMeasureController(IBaseCrudService<UnitOfMeasure, UnitOfMeasureDto, CreateUomRequest, UpdateUomRequest> service)
        : base(service)
    {
    }
}
