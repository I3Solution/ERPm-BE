using System.Text.RegularExpressions;
using ERPm.Application.DTOs.Partner;
using ERPm.Application.Features.Partner;
using ERPm.Domain.Entities.Partner;
using ERPm.Infrastructure.Persistence;
using ERPm.Infrastructure.Services.Common;
using Microsoft.EntityFrameworkCore;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

/// <summary>
/// Delivery addresses of a partner. Every change takes a per-partner lock so the address limit and the single
/// default address hold under concurrent requests.
/// </summary>
public sealed class PartnerDeliveryAddressService(ApplicationDbContext context, PostingTransaction posting) : IPartnerDeliveryAddressService
{
    public const int MaxAddressesPerPartner = 20;
    private static readonly Regex PhoneDigits = new(@"^\+?\d{8,15}$", RegexOptions.Compiled);

    public async Task<List<DeliveryAddressDto>> GetAsync(int partnerId, CancellationToken cancellationToken = default)
    {
        await EnsurePartnerAsync(partnerId, cancellationToken);
        var rows = await context.PartnerDeliveryAddresses.AsNoTracking()
            .Where(a => a.PartnerId == partnerId)
            .OrderByDescending(a => a.IsDefault).ThenBy(a => a.CreatedAtUtc).ThenBy(a => a.Id)
            .ToListAsync(cancellationToken);
        return rows.Select(Map).ToList();
    }

    public async Task<DeliveryAddressDto> GetByIdAsync(int partnerId, int id, CancellationToken cancellationToken = default)
    {
        await EnsurePartnerAsync(partnerId, cancellationToken);
        return Map(await FindAsync(partnerId, id, track: false, cancellationToken));
    }

    public Task<DeliveryAddressDto> CreateAsync(int partnerId, SaveDeliveryAddressRequest request, CancellationToken cancellationToken = default) =>
        posting.RunAsync(async () =>
        {
            await EnsurePartnerAsync(partnerId, cancellationToken);
            await posting.LockAsync($"partner-addresses:{partnerId}", cancellationToken);
            var phone = Validate(request);
            var live = await context.PartnerDeliveryAddresses.Where(a => a.PartnerId == partnerId).ToListAsync(cancellationToken);
            Require(live.Count < MaxAddressesPerPartner, "error.crm.delivery_address_limit", 422);

            // The first address is always the default; later ones only when asked.
            var makeDefault = live.Count == 0 || request.IsDefault;
            if (makeDefault) await ClearDefaultAsync(live, cancellationToken);
            var entity = new PartnerDeliveryAddress { PartnerId = partnerId, IsDefault = makeDefault };
            Apply(entity, request, phone);
            context.PartnerDeliveryAddresses.Add(entity);
            await context.SaveChangesAsync(cancellationToken);
            return Map(entity);
        }, cancellationToken);

    public Task<DeliveryAddressDto> UpdateAsync(int partnerId, int id, SaveDeliveryAddressRequest request, CancellationToken cancellationToken = default) =>
        posting.RunAsync(async () =>
        {
            await EnsurePartnerAsync(partnerId, cancellationToken);
            await posting.LockAsync($"partner-addresses:{partnerId}", cancellationToken);
            var phone = Validate(request);
            var entity = await FindAsync(partnerId, id, track: true, cancellationToken);
            Apply(entity, request, phone);
            // A default address can only be replaced by choosing another one, never lowered.
            if (request.IsDefault && !entity.IsDefault)
            {
                var live = await context.PartnerDeliveryAddresses.Where(a => a.PartnerId == partnerId && a.Id != id).ToListAsync(cancellationToken);
                await ClearDefaultAsync(live, cancellationToken);
                entity.IsDefault = true;
            }
            await context.SaveChangesAsync(cancellationToken);
            return Map(entity);
        }, cancellationToken);

    public Task DeleteAsync(int partnerId, int id, CancellationToken cancellationToken = default) =>
        posting.RunAsync(async () =>
        {
            await EnsurePartnerAsync(partnerId, cancellationToken);
            await posting.LockAsync($"partner-addresses:{partnerId}", cancellationToken);
            var entity = await FindAsync(partnerId, id, track: true, cancellationToken);
            var wasDefault = entity.IsDefault;
            // Orders keep their own copy of the address, so deleting never blocks on past documents.
            entity.IsDeleted = true;
            entity.DeletedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            if (wasDefault)
            {
                var next = await context.PartnerDeliveryAddresses.Where(a => a.PartnerId == partnerId && a.Id != id)
                    .OrderBy(a => a.CreatedAtUtc).ThenBy(a => a.Id).FirstOrDefaultAsync(cancellationToken);
                if (next != null) next.IsDefault = true;
            }
            return true;
        }, cancellationToken);

    public Task<DeliveryAddressDto> SetDefaultAsync(int partnerId, int id, CancellationToken cancellationToken = default) =>
        posting.RunAsync(async () =>
        {
            await EnsurePartnerAsync(partnerId, cancellationToken);
            await posting.LockAsync($"partner-addresses:{partnerId}", cancellationToken);
            var entity = await FindAsync(partnerId, id, track: true, cancellationToken);
            if (!entity.IsDefault)
            {
                var live = await context.PartnerDeliveryAddresses.Where(a => a.PartnerId == partnerId && a.Id != id).ToListAsync(cancellationToken);
                await ClearDefaultAsync(live, cancellationToken);
                entity.IsDefault = true;
                await context.SaveChangesAsync(cancellationToken);
            }
            return Map(entity);
        }, cancellationToken);

    // The partial unique index allows one live default, so the old flag must be saved off before the new one is set.
    private async Task ClearDefaultAsync(IEnumerable<PartnerDeliveryAddress> addresses, CancellationToken ct)
    {
        var previous = addresses.Where(a => a.IsDefault).ToList();
        if (previous.Count == 0) return;
        foreach (var address in previous) address.IsDefault = false;
        await context.SaveChangesAsync(ct);
    }

    private async Task EnsurePartnerAsync(int partnerId, CancellationToken ct) =>
        Require(await context.Partners.AnyAsync(p => p.Id == partnerId, ct), "error.crm.partner_not_found", 404);

    private async Task<PartnerDeliveryAddress> FindAsync(int partnerId, int id, bool track, CancellationToken ct)
    {
        var query = track ? context.PartnerDeliveryAddresses : context.PartnerDeliveryAddresses.AsNoTracking();
        var entity = await query.SingleOrDefaultAsync(a => a.Id == id && a.PartnerId == partnerId, ct);
        Require(entity != null, "error.crm.delivery_address_not_found", 404);
        return entity!;
    }

    /// <summary>Checks the request and returns the phone number in its normalized form.</summary>
    private static string? Validate(SaveDeliveryAddressRequest request)
    {
        Require(!string.IsNullOrWhiteSpace(request.RecipientName) && request.RecipientName.Trim().Length <= 255, "error.crm.delivery_address_recipient_required");
        Require(!string.IsNullOrWhiteSpace(request.Address) && request.Address.Trim().Length <= 500, "error.crm.delivery_address_address_required");
        Require((request.City?.Trim().Length ?? 0) <= 100 && (request.District?.Trim().Length ?? 0) <= 100 && (request.Ward?.Trim().Length ?? 0) <= 100, "error.validation.request");
        if (string.IsNullOrWhiteSpace(request.Phone)) return null;
        var phone = Regex.Replace(request.Phone, @"[\s.\-()]", string.Empty);
        Require(PhoneDigits.IsMatch(phone), "error.crm.delivery_address_phone_invalid");
        return phone;
    }

    private static void Apply(PartnerDeliveryAddress entity, SaveDeliveryAddressRequest request, string? phone)
    {
        entity.RecipientName = request.RecipientName.Trim();
        entity.Phone = phone;
        entity.Address = request.Address.Trim();
        entity.City = Clean(request.City);
        entity.District = Clean(request.District);
        entity.Ward = Clean(request.Ward);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static DeliveryAddressDto Map(PartnerDeliveryAddress a) => new()
    {
        Id = a.Id,
        PartnerId = a.PartnerId,
        RecipientName = a.RecipientName,
        Phone = a.Phone,
        Address = a.Address,
        City = a.City,
        District = a.District,
        Ward = a.Ward,
        IsDefault = a.IsDefault,
        CreatedAt = DateTime.SpecifyKind(a.CreatedAtUtc, DateTimeKind.Utc)
    };
}
