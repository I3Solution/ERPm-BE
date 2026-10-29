using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Partner;
using ERPm.Application.Features.Partner;
using ERPm.Domain.Entities.Partner;
using Microsoft.EntityFrameworkCore;
using ERPm.Domain.Common;
using ERPm.Domain.Entities.Accounting;
using static ERPm.Infrastructure.Services.Common.PostingTransaction;

namespace ERPm.Infrastructure.Services;

public class PartnerService : IPartnerService
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUser? _user;

    public PartnerService(IApplicationDbContext context, ICurrentUser? user = null)
    {
        _context = context;
        _user = user;
    }

    public async Task<List<PartnerDto>> GetPartnersAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Partners
            .OrderBy(p => p.Code)
            .Select(p => new PartnerDto
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
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PartnerDto> CreatePartnerAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        Require(!string.IsNullOrWhiteSpace(request.Name) && request.Name.Length <= 255 && request.Code.Length <= 50 && Enum.IsDefined(request.PartnerType), "error.validation.request");
        ValidateNumber(request.CreditLimit);
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
            IsActive = true,
            Notes = request.Notes
        };

        await _context.Partners.AddAsync(partner, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new PartnerDto
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
            IsActive = partner.IsActive
        };
    }
}
