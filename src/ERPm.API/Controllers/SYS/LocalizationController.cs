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
    /// Lấy danh sách tất cả ngôn ngữ đang hoạt động (vd: vi, en)
    /// </summary>
    [HttpGet("languages")]
    public async Task<ActionResult<ApiResponse<List<LanguageDto>>>> GetLanguages(CancellationToken cancellationToken)
    {
        var result = await _localizationService.GetLanguagesAsync(cancellationToken);
        return Ok(ApiResponse<List<LanguageDto>>.Ok(result));
    }

    /// <summary>
    /// Tải toàn bộ bundle từ điển tĩnh phục vụ client cache
    /// </summary>
    [HttpGet("{langCode}")]
    public async Task<ActionResult<ApiResponse<DictionaryBundleDto>>> GetDictionaryBundle(string langCode, CancellationToken cancellationToken)
    {
        var result = await _localizationService.GetDictionaryBundleAsync(langCode, cancellationToken);
        return Ok(ApiResponse<DictionaryBundleDto>.Ok(result));
    }

    /// <summary>
    /// Thêm hoặc cập nhật một cặp từ điển key-value
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<ApiResponse<bool>>> UpsertDictionaryEntry([FromBody] UpsertDictionaryRequest request, CancellationToken cancellationToken)
    {
        var success = await _localizationService.UpsertDictionaryEntryAsync(request, cancellationToken);
        return Ok(ApiResponse<bool>.Ok(success, "Dictionary entry updated successfully."));
    }
}
