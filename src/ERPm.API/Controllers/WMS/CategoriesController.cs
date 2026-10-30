using ERPm.API.Security;
using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.Catalog;
using ERPm.Domain.Entities.Catalog;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.WMS;

/// <summary>
/// Manages item categories (WMS Module).
/// Inherits BaseCrudController for full type-safe CRUD endpoints.
/// </summary>
[PermissionResource("wms:category")]
[Route("api/v1/wms/categories")]
public class CategoriesController : BaseCrudController<Category, CategoryDto, CreateCategoryRequest, UpdateCategoryRequest>
{
    public CategoriesController(IBaseCrudService<Category, CategoryDto, CreateCategoryRequest, UpdateCategoryRequest> service)
        : base(service)
    {
    }
}
