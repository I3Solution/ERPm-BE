namespace ERPm.Domain.Services.Sales;

public sealed record PriceTier(
    decimal MinQuantity,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount = 0m);

public sealed record PriceCandidate(
    int PriceListId,
    string PriceListName,
    bool IsCustomerDefault,
    bool IsBranchSpecific,
    DateTime EffectiveFrom,
    IReadOnlyList<PriceTier> Tiers,
    int? PriceListVersionId = null,
    string? VersionCode = null,
    bool IsTaxInclusive = false,
    bool IsExplicit = false);

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
    bool IsTaxInclusive,
    decimal FixedDiscountAmount = 0m);

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
            return Result(null, null, null, null, catalogPrice, catalogPrice, 0m, 0m, quantity, vatRate, false);

        var chosen = matches.Any(m => m.Candidate.IsExplicit)
            ? matches.Where(m => m.Candidate.IsExplicit).First()
            : matches.Any(m => m.Candidate.IsCustomerDefault)
                ? matches.Where(m => m.Candidate.IsCustomerDefault).First()
                : matches
                    .OrderBy(m => NetLine(m.Tier, quantity, m.Candidate.IsTaxInclusive, vatRate))
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
            chosen.Tier.DiscountAmount,
            quantity,
            vatRate,
            chosen.Candidate.IsTaxInclusive);
    }

    private static PriceTier? BestTier(IReadOnlyList<PriceTier> tiers, decimal quantity) =>
        tiers.Where(t => t.MinQuantity <= quantity).OrderByDescending(t => t.MinQuantity).FirstOrDefault();

    private static decimal NetLine(PriceTier tier, decimal quantity, bool taxInclusive, decimal vatRate)
    {
        var unit = taxInclusive && vatRate > 0m ? Round(tier.UnitPrice / (1m + vatRate / 100m)) : tier.UnitPrice;
        var gross = Round(unit * quantity);
        var percentDiscount = Round(gross * tier.DiscountPercent / 100m);
        var fixedDiscount = taxInclusive && vatRate > 0m
            ? Round(tier.DiscountAmount / (1m + vatRate / 100m))
            : Round(tier.DiscountAmount);
        var discount = Math.Min(gross, percentDiscount + fixedDiscount);
        return gross - discount;
    }

    private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static PricingResult Result(
        int? listId, string? listName, int? versionId, string? versionCode,
        decimal original, decimal enteredUnitPrice, decimal discountPercent, decimal enteredFixedDiscount,
        decimal quantity, decimal vatRate, bool taxInclusive)
    {
        var unitPrice = taxInclusive && vatRate > 0m
            ? Round(enteredUnitPrice / (1m + vatRate / 100m))
            : enteredUnitPrice;
        var gross = Round(unitPrice * quantity);
        var percentDiscount = Round(gross * discountPercent / 100m);
        var fixedDiscount = taxInclusive && vatRate > 0m
            ? Round(enteredFixedDiscount / (1m + vatRate / 100m))
            : Round(enteredFixedDiscount);
        var discount = Math.Min(gross, percentDiscount + fixedDiscount);
        var lineTotal = gross - discount;
        var tax = Round(lineTotal * vatRate / 100m);
        return new PricingResult(listId, listName, original, unitPrice, discountPercent, discount, lineTotal,
            versionId, versionCode, vatRate, tax, lineTotal + tax, taxInclusive, fixedDiscount);
    }
}
