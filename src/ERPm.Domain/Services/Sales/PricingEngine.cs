namespace ERPm.Domain.Services.Sales;

/// <summary>One quantity tier of an item inside a price list.</summary>
public sealed record PriceTier(decimal MinQuantity, decimal UnitPrice, decimal DiscountPercent);

/// <summary>A price list that is already known to be applicable (active, in date range, for the branch) and its tiers for one item.</summary>
public sealed record PriceCandidate(
    int PriceListId,
    string PriceListName,
    bool IsCustomerDefault,
    bool IsBranchSpecific,
    DateTime EffectiveFrom,
    IReadOnlyList<PriceTier> Tiers);

public sealed record PricingResult(
    int? AppliedPriceListId,
    string? AppliedPriceListName,
    decimal OriginalPrice,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal LineTotal);

/// <summary>
/// Picks the selling price of one item line. Pure function: loading and filtering price lists is the caller's job.
/// </summary>
/// <remarks>
/// Rules, in order:
/// 1. Inside a list the tier with the highest <c>MinQuantity</c> not above the quantity wins.
/// 2. The customer's own price list wins when it prices the item.
/// 3. Otherwise the list with the lowest net unit price (price after tier discount) wins; ties prefer a branch-specific
///    list, then the most recently effective one, then the lowest id.
/// 4. With no matching list the catalogue price is used unchanged.
/// </remarks>
public static class PricingEngine
{
    public static PricingResult Price(decimal catalogPrice, decimal quantity, IEnumerable<PriceCandidate> candidates)
    {
        var matches = candidates
            .Select(c => (Candidate: c, Tier: BestTier(c.Tiers, quantity)))
            .Where(m => m.Tier != null)
            .Select(m => (m.Candidate, Tier: m.Tier!))
            .ToList();

        if (matches.Count == 0) return Result(null, null, catalogPrice, catalogPrice, 0m, quantity);

        var chosen = matches.Any(m => m.Candidate.IsCustomerDefault)
            ? matches.Where(m => m.Candidate.IsCustomerDefault).First()
            : matches
                .OrderBy(m => Net(m.Tier))
                .ThenByDescending(m => m.Candidate.IsBranchSpecific)
                .ThenByDescending(m => m.Candidate.EffectiveFrom)
                .ThenBy(m => m.Candidate.PriceListId)
                .First();

        return Result(chosen.Candidate.PriceListId, chosen.Candidate.PriceListName, catalogPrice, chosen.Tier.UnitPrice, chosen.Tier.DiscountPercent, quantity);
    }

    private static PriceTier? BestTier(IReadOnlyList<PriceTier> tiers, decimal quantity) =>
        tiers.Where(t => t.MinQuantity <= quantity).OrderByDescending(t => t.MinQuantity).FirstOrDefault();

    private static decimal Net(PriceTier tier) => tier.UnitPrice * (1m - tier.DiscountPercent / 100m);

    private static decimal Round(decimal value) => decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    private static PricingResult Result(int? listId, string? listName, decimal original, decimal unitPrice, decimal discountPercent, decimal quantity)
    {
        var gross = Round(unitPrice * quantity);
        var discount = Round(gross * discountPercent / 100m);
        return new PricingResult(listId, listName, original, unitPrice, discountPercent, discount, gross - discount);
    }
}
