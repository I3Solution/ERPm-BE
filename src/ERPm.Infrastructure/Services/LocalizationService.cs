using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Localization;
using ERPm.Application.Features.Localization;
using ERPm.Domain.Entities.Localization;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

public class LocalizationService : ILocalizationService
{
    private readonly IApplicationDbContext _context;

    public LocalizationService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<LanguageDto>> GetLanguagesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AppLanguages
            .Where(l => l.IsActive)
            .OrderBy(l => l.SortOrder)
            .Select(l => new LanguageDto
            {
                Code = l.Code,
                Name = l.Name,
                FlagIcon = l.FlagIcon,
                IsDefault = l.IsDefault,
                IsActive = l.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<DictionaryBundleDto> GetDictionaryBundleAsync(string langCode, CancellationToken cancellationToken = default)
    {
        var entries = await _context.AppDictionaries
            .Where(d => d.LanguageCode == langCode)
            .ToDictionaryAsync(d => d.Key, d => d.Value, cancellationToken);

        return new DictionaryBundleDto
        {
            LanguageCode = langCode,
            Entries = entries
        };
    }

    public async Task<bool> UpsertDictionaryEntryAsync(UpsertDictionaryRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await _context.AppDictionaries
            .FirstOrDefaultAsync(d => d.LanguageCode == request.LanguageCode && d.Key == request.Key, cancellationToken);

        if (existing != null)
        {
            existing.Value = request.Value;
            existing.Category = request.Category;
        }
        else
        {
            var entry = new AppDictionary
            {
                LanguageCode = request.LanguageCode,
                Category = request.Category,
                Key = request.Key,
                Value = request.Value
            };
            await _context.AppDictionaries.AddAsync(entry, cancellationToken);
        }

        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }
}
