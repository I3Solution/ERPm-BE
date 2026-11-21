using ERPm.Application.Common;
using ERPm.Application.DTOs.System;

namespace ERPm.Application.Features.System;

/// <summary>Typed branch settings: catalog, overrides inherited through the branch tree inside a company, and history.</summary>
public interface IBranchSettingService
{
    Task<List<SettingDefinitionDto>> GetDefinitionsAsync(string? group, CancellationToken ct = default);
    Task<SettingDefinitionDto> CreateDefinitionAsync(CreateSettingDefinitionRequest request, CancellationToken ct = default);
    Task<SettingListDto> GetSettingsAsync(int branchId, string? group, CancellationToken ct = default);
    Task<SettingViewDto> GetSettingAsync(int branchId, string key, CancellationToken ct = default);
    Task<SettingImpactDto> PreviewAsync(int branchId, string key, UpdateBranchSettingRequest request, string? ifMatch, CancellationToken ct = default);
    Task<SettingViewDto> UpdateAsync(int branchId, string key, UpdateBranchSettingRequest request, string? ifMatch, CancellationToken ct = default);
    Task<PagedResult<SettingHistoryItemDto>> GetHistoryAsync(int branchId, string key, int pageIndex, int pageSize, CancellationToken ct = default);
}

/// <summary>
/// Supplies the effective view of a module-managed key (storageKind = module). The module owns writes and its editor;
/// SYS only lists the value in the settings page.
/// </summary>
public interface IBranchSettingModuleProvider
{
    string Key { get; }
    Task<SettingViewDto> GetAsync(int branchId, CancellationToken ct = default);
}
