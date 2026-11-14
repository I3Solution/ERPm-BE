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
            .OrderByDescending(a => a.IsDefault)
            .ThenBy(a => a.AddressType)
            .ThenBy(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id)
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

            var sameType = live.Where(a => a.AddressType == request.AddressType).ToList();
            // The first live address of a type is always that type's default; later ones only when asked.
            var makeDefault = sameType.Count == 0 || request.IsDefault;
            if (makeDefault) await ClearDefaultAsync(sameType, cancellationToken);
            var entity = new PartnerDeliveryAddress
            {
                PartnerId = partnerId,
                AddressType = request.AddressType,
                IsDefault = makeDefault
            };
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
            var previousType = entity.AddressType;
            Apply(entity, request, phone);

            if (previousType == request.AddressType)
            {
                // A default address can only be replaced by choosing another of the same type, never lowered.
                if (request.IsDefault && !entity.IsDefault)
                {
                    var peers = await PeersAsync(partnerId, entity.AddressType, excludeId: id, cancellationToken);
                    await ClearDefaultAsync(peers, cancellationToken);
                    entity.IsDefault = true;
                }
            }
            else
            {
                // Save off the old default flag before switching types so neither type violates the unique default index.
                var wasDefault = entity.IsDefault;
                if (wasDefault)
                {
                    entity.IsDefault = false;
                    await context.SaveChangesAsync(cancellationToken);
                    await PromoteOldestDefaultAsync(partnerId, previousType, excludeId: id, cancellationToken);
                }

                var targetPeers = await PeersAsync(partnerId, request.AddressType, excludeId: id, cancellationToken);
                if (targetPeers.Count == 0 || request.IsDefault)
                {
                    await ClearDefaultAsync(targetPeers, cancellationToken);
                    entity.IsDefault = true;
                }
                else
                {
                    entity.IsDefault = false;
                }
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
            var deletedType = entity.AddressType;
            // Orders keep their own copy of the address, so deleting never blocks on past documents.
            entity.IsDeleted = true;
            entity.DeletedAtUtc = DateTime.UtcNow;
            await context.SaveChangesAsync(cancellationToken);
            if (wasDefault) await PromoteOldestDefaultAsync(partnerId, deletedType, excludeId: id, cancellationToken);
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
                var peers = await PeersAsync(partnerId, entity.AddressType, excludeId: id, cancellationToken);
                await ClearDefaultAsync(peers, cancellationToken);
                entity.IsDefault = true;
                await context.SaveChangesAsync(cancellationToken);
            }
            return Map(entity);
        }, cancellationToken);

    private Task<List<PartnerDeliveryAddress>> PeersAsync(int partnerId, AddressType addressType, int excludeId, CancellationToken ct) =>
        context.PartnerDeliveryAddresses
            .Where(a => a.PartnerId == partnerId && a.AddressType == addressType && a.Id != excludeId)
            .ToListAsync(ct);

    private async Task PromoteOldestDefaultAsync(int partnerId, AddressType addressType, int excludeId, CancellationToken ct)
    {
        var next = await context.PartnerDeliveryAddresses
            .Where(a => a.PartnerId == partnerId && a.AddressType == addressType && a.Id != excludeId)
            .OrderBy(a => a.CreatedAtUtc)
            .ThenBy(a => a.Id)
            .FirstOrDefaultAsync(ct);
        if (next == null) return;
        next.IsDefault = true;
        await context.SaveChangesAsync(ct);
    }

    // The partial unique index allows one live default per type, so the old flag must be saved off before the new one is set.
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
        Require(Enum.IsDefined(request.AddressType), "error.validation.request");
        Require(!string.IsNullOrWhiteSpace(request.RecipientName), "error.crm.delivery_address_recipient_required");
        Require(request.RecipientName.Trim().Length <= 255, "error.crm.delivery_address_recipient_too_long");
        Require(!string.IsNullOrWhiteSpace(request.Address), "error.crm.delivery_address_address_required");
        Require(request.Address.Trim().Length <= 500, "error.crm.delivery_address_address_too_long");
        Require((request.City?.Trim().Length ?? 0) <= 100, "error.crm.delivery_address_city_too_long");
        Require((request.District?.Trim().Length ?? 0) <= 100, "error.crm.delivery_address_district_too_long");
        Require((request.Ward?.Trim().Length ?? 0) <= 100, "error.crm.delivery_address_ward_too_long");
        if (string.IsNullOrWhiteSpace(request.Phone)) return null;
        var phone = Regex.Replace(request.Phone, @"[\s.\-()]", string.Empty);
        Require(PhoneDigits.IsMatch(phone), "error.crm.delivery_address_phone_invalid");
        return phone;
    }

    private static void Apply(PartnerDeliveryAddress entity, SaveDeliveryAddressRequest request, string? phone)
    {
        entity.AddressType = request.AddressType;
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
        AddressType = a.AddressType,
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
