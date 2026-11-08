using ERPm.Application.Common;
using ERPm.Application.Common.Interfaces;
using ERPm.Application.Common.Notifications;
using ERPm.Application.DTOs.Partner;
using ERPm.Application.Features.Partner;
using ERPm.Application.Features.System;
using ERPm.Infrastructure.Persistence;
using ERPm.Domain.Entities.Partner;
using Microsoft.EntityFrameworkCore;
using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

public class PartnerService : IPartnerService
{
    public const string UnlimitedCreditAction = "crm:partner:unlimited-credit";

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUser? _user;
    private readonly IIamService? _iam;
    private readonly INotificationEngine? _notificationEngine;

    public PartnerService(ApplicationDbContext context, ICurrentUser? user = null, IIamService? iam = null, INotificationEngine? notificationEngine = null)
    {
        _context = context;
        _user = user;
        _iam = iam;
        _notificationEngine = notificationEngine;
    }

    public async Task<List<PartnerDto>> GetPartnersAsync(CancellationToken cancellationToken = default) =>
        await Projection(_context.Partners).OrderBy(p => p.Code).ToListAsync(cancellationToken);

    public async Task<PartnerDto> GetPartnerAsync(int partnerId, CancellationToken cancellationToken = default)
    {
        var partner = await Projection(_context.Partners.Where(p => p.Id == partnerId)).SingleOrDefaultAsync(cancellationToken);
        Require(partner != null, "error.crm.partner_not_found", 404);
        return partner!;
    }

    /// <summary>
    /// Changes the descriptive fields, the credit limit, the price list and the active flag. The code and debt balance
    /// are kept; who changed it and when is recorded by the standard auditing.
    /// </summary>
    public async Task<PartnerDto> UpdatePartnerAsync(int partnerId, UpdatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        Require(!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length <= 255 && request.PartnerType.HasValue && Enum.IsDefined(request.PartnerType.Value)
            && request.CreditLimit.HasValue && request.IsActive.HasValue, "error.validation.request");
        Require(string.IsNullOrWhiteSpace(request.Email) || (System.Net.Mail.MailAddress.TryCreate(request.Email.Trim(), out var mail) && mail.Address == request.Email.Trim()), "error.validation.request");
        Require((request.Phone?.Length ?? 0) <= 50 && (request.Email?.Length ?? 0) <= 100 && (request.TaxCode?.Length ?? 0) <= 50
            && (request.Address?.Length ?? 0) <= 500 && (request.Notes?.Length ?? 0) <= 2000, "error.validation.request");
        ValidateNumber(request.CreditLimit!.Value);
        var partner = await _context.Partners.SingleOrDefaultAsync(p => p.Id == partnerId, cancellationToken);
        Require(partner != null, "error.crm.partner_not_found", 404);
        // Keeping a list that was switched off since is allowed; choosing one needs it to be active.
        Require(request.PriceListId == null || request.PriceListId == partner!.PriceListId
            || await _context.PriceLists.AnyAsync(l => l.Id == request.PriceListId && l.IsActive, cancellationToken), "error.validation.reference");
        partner!.Name = request.Name.Trim();
        partner.PartnerType = request.PartnerType!.Value;
        partner.Phone = Clean(request.Phone);
        partner.Email = Clean(request.Email);
        partner.Address = Clean(request.Address);
        partner.TaxCode = Clean(request.TaxCode);
        partner.CreditLimit = request.CreditLimit!.Value;
        partner.PriceListId = request.PriceListId;
        partner.Notes = Clean(request.Notes);
        partner.IsActive = request.IsActive!.Value;
        await _context.SaveChangesAsync(cancellationToken);
        return await GetPartnerAsync(partnerId, cancellationToken);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IQueryable<PartnerDto> Projection(IQueryable<Partner> partners) =>
            partners.Select(p => new PartnerDto
            {
                Id = p.Id,
                Code = p.Code,
                Name = p.Name,
                PartnerType = p.PartnerType,
                Phone = p.Phone,
                Email = p.Email,
                Address = p.Address,
                TaxCode = p.TaxCode,
                CreditLimit = p.CreditLimit,
                DebtBalance = _user == null || _user.IsAdmin || _user.IsSystem ? p.DebtBalance :
                    _context.SalesInvoices.Where(i => i.CustomerId == p.Id && i.Status == VoucherStatus.Posted).Sum(i => i.RemainingDebt) -
                    _context.StockReceipts.Where(r => r.SupplierId == p.Id && r.Status == VoucherStatus.Posted).Sum(r => r.TotalAmount),
                PriceListId = p.PriceListId,
                IsUnlimitedCredit = p.IsUnlimitedCredit,
                Notes = p.Notes,
                IsActive = p.IsActive
            });

    public async Task<PartnerDto> CreatePartnerAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        Require(!string.IsNullOrWhiteSpace(request.Name) && request.Name.Length <= 255 && request.Code.Length <= 50 && Enum.IsDefined(request.PartnerType), "error.validation.request");
        ValidateNumber(request.CreditLimit);
        if (request.IsUnlimitedCredit) await RequireUnlimitedCreditRightAsync(cancellationToken);
        Require(request.PriceListId == null || await _context.PriceLists.AnyAsync(l => l.Id == request.PriceListId && l.IsActive, cancellationToken), "error.validation.reference");
        var partner = new Partner
        {
            Code = string.IsNullOrWhiteSpace(request.Code) ? $"PT{Uuid7Generator.NewUuid7():N}" : request.Code.Trim().ToUpperInvariant(),
            Name = request.Name,
            PartnerType = request.PartnerType,
            Phone = request.Phone,
            Email = request.Email,
            Address = request.Address,
            TaxCode = request.TaxCode,
            CreditLimit = request.CreditLimit,
            DebtBalance = 0m,
            PriceListId = request.PriceListId,
            IsUnlimitedCredit = request.IsUnlimitedCredit,
            IsActive = true,
            Notes = request.Notes
        };

        await _context.Partners.AddAsync(partner, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
        if (partner.IsUnlimitedCredit) await AnnounceUnlimitedCreditAsync(partner, cancellationToken);
        return ToDto(partner);
    }

    public async Task<PartnerDto> SetUnlimitedCreditAsync(int partnerId, bool? requested, CancellationToken cancellationToken = default)
    {
        // Checked before anything is read or saved, so a malformed request changes nothing and notifies nobody.
        Require(requested.HasValue, "error.crm.unlimited_credit_value_required");
        var isUnlimitedCredit = requested!.Value;
        var partner = await _context.Partners.SingleOrDefaultAsync(p => p.Id == partnerId, cancellationToken);
        Require(partner != null, "error.crm.partner_not_found", 404);
        // Sending the value the partner already has changes nothing, so it needs no right and raises no notice.
        if (partner!.IsUnlimitedCredit == isUnlimitedCredit) return ToDto(partner);
        await RequireUnlimitedCreditRightAsync(cancellationToken);
        partner.IsUnlimitedCredit = isUnlimitedCredit;
        await _context.SaveChangesAsync(cancellationToken);
        await AnnounceUnlimitedCreditAsync(partner, cancellationToken);
        return ToDto(partner);
    }

    // System calls (seeding, background jobs) have no signed-in principal and are not restricted.
    private async Task RequireUnlimitedCreditRightAsync(CancellationToken ct)
    {
        if (_iam == null) return;
        var principal = await _iam.GetCurrentPrincipalAsync(ct);
        Require(principal == null || principal.Can(UnlimitedCreditAction), "error.crm.unlimited_credit_forbidden", 403);
    }

    // Sent after the change is saved, to everyone who may switch the flag except the person who just did.
    private async Task AnnounceUnlimitedCreditAsync(Partner partner, CancellationToken ct)
    {
        if (_notificationEngine == null || _iam == null) return;
        int.TryParse(_user?.UserId, out var actorId);
        var userIds = await _context.AppUsers.AsNoTracking().Where(u => u.IsActive && u.Id != actorId).Select(u => u.Id).ToListAsync(ct);
        foreach (var userId in userIds)
        {
            if (!(await _iam.GetPrincipalAsync(userId, ct)).Can(UnlimitedCreditAction)) continue;
            _ = _notificationEngine.PublishAsync(new ERPm.Domain.Events.NotificationEvent(
                "CRM_PARTNER_UNLIMITED_CREDIT_CHANGED",
                ERPm.Domain.Enums.NotificationCategory.CRM,
                partner.IsUnlimitedCredit ? ERPm.Domain.Enums.NotificationSeverity.WARNING : ERPm.Domain.Enums.NotificationSeverity.INFO,
                ERPm.Domain.Enums.NotificationTargetType.User,
                targetId: userId,
                parameters: new Dictionary<string, object>
                {
                    ["partner_code"] = partner.Code,
                    ["partner_name"] = partner.Name,
                    ["state"] = partner.IsUnlimitedCredit ? "ON" : "OFF"
                },
                actionUrl: "/crm/partners"));
        }
    }

    private static PartnerDto ToDto(Partner partner) => new()
    {
        Id = partner.Id,
        Code = partner.Code,
        Name = partner.Name,
        PartnerType = partner.PartnerType,
        Phone = partner.Phone,
        Email = partner.Email,
        Address = partner.Address,
        TaxCode = partner.TaxCode,
        CreditLimit = partner.CreditLimit,
        DebtBalance = partner.DebtBalance,
        PriceListId = partner.PriceListId,
        IsUnlimitedCredit = partner.IsUnlimitedCredit,
        Notes = partner.Notes,
        IsActive = partner.IsActive
    };
}
