using ERPm.API.Controllers.Base;
using ERPm.Application.Common.Crud;
using ERPm.Application.DTOs.System;
using ERPm.Domain.Entities.System;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers.SYS;

/// <summary>
/// Manages legal entities (SYS Module), shared by HRM, accounting and branches.
/// </summary>
[Route("api/v1/sys/companies")]
public class CompaniesController : BaseCrudController<Company, CompanyDto, CreateCompanyRequest, UpdateCompanyRequest>
{
    public CompaniesController(IBaseCrudService<Company, CompanyDto, CreateCompanyRequest, UpdateCompanyRequest> service)
        : base(service)
    {
    }
}
