using ERPm.Application.DTOs.Localization;

namespace ERPm.Application.Features.Localization;

public interface ILocalizationService
{
    Task<List<LanguageDto>> GetLanguagesAsync(CancellationToken cancellationToken = default);
    Task<DictionaryBundleDto> GetDictionaryBundleAsync(string langCode, CancellationToken cancellationToken = default);
    Task<bool> UpsertDictionaryEntryAsync(UpsertDictionaryRequest request, CancellationToken cancellationToken = default);
}
