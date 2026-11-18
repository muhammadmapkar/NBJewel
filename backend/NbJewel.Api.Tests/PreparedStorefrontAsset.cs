// Role: Seeds a controlled reviewed asset for HTTP boundary tests. Real Python generation
// and manual approval are exercised separately by ItemCutoutHttpTests.
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;
namespace NbJewel.Api.Tests;
internal static class PreparedStorefrontAsset
{
    public static async Task<ItemImageAsset> Create(KnowledgeHttpFactory factory, int itemId)
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<ItemCutoutOptions>();
        var revision = Guid.NewGuid().ToString("N");
        var folder = options.RevisionFolder(revision); Directory.CreateDirectory(folder);
        var bytes = Jewelry.JewelryTestKit.CutoutPhoto;
        await File.WriteAllBytesAsync(Path.Combine(folder, "preview.png"), bytes);
        var hash = Convert.ToHexString(SHA256.HashData(bytes));
        var asset = new ItemImageAsset(itemId, revision, hash, hash, "ready", "approved", "open", 64, 64, DateTimeOffset.UtcNow);
        await scope.ServiceProvider.GetRequiredService<IItemImageAssetRepository>().SaveAsync(asset, default);
        var repo = scope.ServiceProvider.GetRequiredService<IItemRepository>(); var item = (await repo.GetItemAsync(itemId, default))!;
        await repo.UpdateAsync(itemId, new(item.Code, item.Name, item.CategoryId, item.DesignId, item.MetalType, item.Purity,
            item.GrossWt, item.NetWt, item.StoneWt, item.MakingType, item.MakingValue, item.Huid, item.ImageUrl,
            item.Storefront ?? new StorefrontMetadata { Description = "Assumed sample product", Collection = "Sample" }), default);
        return asset;
    }
}
