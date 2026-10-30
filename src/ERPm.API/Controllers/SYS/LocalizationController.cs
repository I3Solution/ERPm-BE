using ERPm.API.Security;
using ERPm.Application.Common;
using ERPm.Application.DTOs.Localization;
using ERPm.Application.Features.Localization;
using Microsoft.AspNetCore.Mvc;

namespace ERPm.API.Controllers;

public class LocalizationController : BaseApiController
{
    private readonly ILocalizationService _localizationService;

    public LocalizationController(ILocalizationService localizationService)
    {
        _localizationService = localizationService;
    }

    /// <summary>
    /// Gets all active system languages (e.g. vi, en).
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet("languages")]
    public async Task<ActionResult<ApiResponse<List<LanguageDto>>>> GetLanguages(CancellationToken cancellationToken)
    {
        var result = await _localizationService.GetLanguagesAsync(cancellationToken);
        return Ok(ApiResponse<List<LanguageDto>>.Ok(result));
    }

    /// <summary>
    /// Loads the full static dictionary bundle for client caching.
    /// </summary>
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    [HttpGet("{langCode}")]
    public async Task<ActionResult<ApiResponse<DictionaryBundleDto>>> GetDictionaryBundle(string langCode, CancellationToken cancellationToken)
    {
        var result = await _localizationService.GetDictionaryBundleAsync(langCode, cancellationToken);
        return Ok(ApiResponse<DictionaryBundleDto>.Ok(result));
    }

    /// <summary>
    /// Inserts or updates a dictionary key-value pair.
    /// </summary>
    [RequirePermission("sys:localization:edit")]
    [HttpPost]
    public async Task<ActionResult<ApiResponse<bool>>> UpsertDictionaryEntry([FromBody] UpsertDictionaryRequest request, CancellationToken cancellationToken)
    {
        var success = await _localizationService.UpsertDictionaryEntryAsync(request, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(success, "common.saved"));
    }
}
