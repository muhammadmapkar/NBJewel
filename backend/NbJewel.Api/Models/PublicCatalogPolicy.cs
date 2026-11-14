// Role: Shares the assumed public catalog eligibility rules between storefront reads and assistant evidence.
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Models;

public static class PublicCatalogPolicy
{
    private static readonly Dictionary<string, string> Categories = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Necklaces"] = "necklace", ["Earrings"] = "earrings"
    };

    public static async Task<Dictionary<int, string>> AllowedCategoriesAsync(IItemRepository items, CancellationToken ct) =>
        (await items.GetCategoriesAsync(ct)).Where(x => Categories.ContainsKey(x.Name))
            .ToDictionary(x => x.Id, x => Categories[x.Name]);

    public static bool AllowedMetal(string metal) =>
        metal.Equals("Gold", StringComparison.OrdinalIgnoreCase) ||
        metal.Equals("Silver", StringComparison.OrdinalIgnoreCase) ||
        metal.Equals("Platinum", StringComparison.OrdinalIgnoreCase);

    public static bool Allows(Item item, IReadOnlyDictionary<int, string> categories) =>
        item.Status.Equals(ItemStatusRules.Complete, StringComparison.OrdinalIgnoreCase) &&
        categories.ContainsKey(item.CategoryId) && AllowedMetal(item.MetalType);
}
