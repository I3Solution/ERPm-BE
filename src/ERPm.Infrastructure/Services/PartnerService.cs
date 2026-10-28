using ERPm.Application.Common.Interfaces;
using ERPm.Application.DTOs.Partner;
using ERPm.Application.Features.Partner;
using ERPm.Domain.Entities.Partner;
using Microsoft.EntityFrameworkCore;

namespace ERPm.Infrastructure.Services;

public class PartnerService : IPartnerService
{
    private readonly IApplicationDbContext _context;

    public PartnerService(IApplicationDbContext context)
    {
        _context = context;
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
                DebtBalance = p.DebtBalance,
                IsActive = p.IsActive
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<PartnerDto> CreatePartnerAsync(CreatePartnerRequest request, CancellationToken cancellationToken = default)
    {
        var partner = new Partner
        {
            Code = request.Code,
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
