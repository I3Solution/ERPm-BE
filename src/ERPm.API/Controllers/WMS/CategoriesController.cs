using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Catalog;
using ERPm.Domain.Entities.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.WMS;

/// <summary>
/// Quản lý Danh mục hàng hóa (WMS Module)
/// Kế thừa BaseCrudController: Tự động có trọn bộ CRUD Paged, GetById, Create, Update, Delete chuẩn 100% Type-Safe.
/// </summary>
[Route("api/v1/wms/categories")]
public class CategoriesController : BaseCrudController<Category, CategoryDto, CreateCategoryRequest, UpdateCategoryRequest>
{
    public CategoriesController(IBaseCrudService<Category, CategoryDto, CreateCategoryRequest, UpdateCategoryRequest> service)
        : base(service)
    {
    }
}
