namespace ERPm.Domain.Services.Sales;

public sealed record PriceTier(decimal MinQuantity, decimal UnitPrice, decimal DiscountPercent);

public sealed record PriceCandidate(
    int PriceListId,
    string PriceListName,
    bool IsCustomerDefault,
    bool IsBranchSpecific,
    DateTime EffectiveFrom,
    IReadOnlyList<PriceTier> Tiers,
    int? PriceListVersionId = null,
    string? VersionCode = null,
    bool IsTaxInclusive = false);

public sealed record PricingResult(
    int? AppliedPriceListId,
    string? AppliedPriceListName,
    decimal OriginalPrice,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal LineTotal,
    int? AppliedPriceListVersionId,
    string? AppliedVersionCode,
    decimal VatRate,
    decimal TaxAmount,
    decimal TotalWithTax,
    bool IsTaxInclusive);

/// <summary>Pure pricing rules; date and branch filtering are performed by the application service.</summary>
public static class PricingEngine
{
    public static PricingResult Price(decimal catalogPrice, decimal quantity, IEnumerable<PriceCandidate> candidates, decimal vatRate = 0m)
    {
        var matches = candidates
            .Select(c => (Candidate: c, Tier: BestTier(c.Tiers, quantity)))
            .Where(m => m.Tier != null)
            .Select(m => (m.Candidate, Tier: m.Tier!))
            .ToList();

        if (matches.Count == 0)
            return Result(null, null, null, null, catalogPrice, catalogPrice, 0m, quantity, vatRate, false);

        var chosen = matches.Any(m => m.Candidate.IsCustomerDefault)
            ? matches.Where(m => m.Candidate.IsCustomerDefault).First()
            : matches
                .OrderBy(m => Net(m.Tier, m.Candidate.IsTaxInclusive, vatRate))
                .ThenByDescending(m => m.Candidate.IsBranchSpecific)
                .ThenByDescending(m => m.Candidate.EffectiveFrom)
                .ThenBy(m => m.Candidate.PriceListId)
                .First();

        return Result(
            chosen.Candidate.PriceListId,
            chosen.Candidate.PriceListName,
            chosen.Candidate.PriceListVersionId,
            chosen.Candidate.VersionCode,
            catalogPrice,
            chosen.Tier.UnitPrice,
            chosen.Tier.DiscountPercent,
            quantity,
            vatRate,
            chosen.Candidate.IsTaxInclusive);
    }

    private static PriceTier? BestTier(IReadOnlyList<PriceTier> tiers, decimal quantity) =>
        tiers.Where(t => t.MinQuantity <= quantity).OrderByDescending(t => t.MinQuantity).FirstOrDefault();

    private static decimal Net(PriceTier tier, bool taxInclusive, decimal vatRate)
    {
        var unit = taxInclusive && vatRate > 0m ? tier.UnitPrice / (1m + vatRate / 100m) : tier.UnitPrice;
        return unit * (1m - tier.DiscountPercent / 100m);
    }

    private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static PricingResult Result(
        int? listId, string? listName, int? versionId, string? versionCode,
        decimal original, decimal enteredUnitPrice, decimal discountPercent,
        decimal quantity, decimal vatRate, bool taxInclusive)
    {
        var unitPrice = taxInclusive && vatRate > 0m
            ? Round(enteredUnitPrice / (1m + vatRate / 100m))
            : enteredUnitPrice;
        var gross = Round(unitPrice * quantity);
        var discount = Round(gross * discountPercent / 100m);
        var lineTotal = gross - discount;
        var tax = Round(lineTotal * vatRate / 100m);
        return new PricingResult(listId, listName, original, unitPrice, discountPercent, discount, lineTotal,
            versionId, versionCode, vatRate, tax, lineTotal + tax, taxInclusive);
    }
}
