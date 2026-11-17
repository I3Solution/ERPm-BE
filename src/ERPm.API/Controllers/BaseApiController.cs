using ERPm.Application.Common.Transfer;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public abstract class BaseApiController : ControllerBase
{
    /// <summary>
    /// Returns a binary Excel (.xlsx) response with Content-Disposition filename metadata.
    /// </summary>
    protected FileContentResult ExcelFile(ExportFileResult result) =>
        File(result.Content, result.ContentType, result.FileName);
}
