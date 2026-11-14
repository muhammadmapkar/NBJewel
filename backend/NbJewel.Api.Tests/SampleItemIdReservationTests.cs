// Role: Checks restart/legacy-storage ID reservations without making sample item records durable.
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;
namespace NbJewel.Api.Tests;

public sealed class SampleItemIdReservationTests
{
    [Fact]
    public async Task LegacyPersistedIdsAndReservationsRemainUnavailableToNewItems()
    {
        var folder = Path.Combine(Path.GetTempPath(), "nbjewel-id-reservation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(folder, "links"));
        Directory.CreateDirectory(Path.Combine(folder, "publications"));
        // Only names are needed to reserve old IDs; no asset data or publication is invented.
        await File.WriteAllTextAsync(Path.Combine(folder, "links", "item-40.json"), "{}");
        await File.WriteAllTextAsync(Path.Combine(folder, "publications", "item-50.json"), "{}");
        await using var factory = new KnowledgeHttpFactory();
        var environment = factory.Services.GetRequiredService<IWebHostEnvironment>();
        var options = new ItemCutoutOptions(folder, "unused", "unused");
        var first = new SampleItemRepository(environment, options);
        var item = (await first.GetItemAsync(7, default))!;
        var request = new ItemCreateRequest("SAMPLE-RESERVATION", "Assumed sample", item.CategoryId, item.DesignId,
            item.MetalType, item.Purity, item.GrossWt, item.NetWt, item.StoneWt, item.MakingType, item.MakingValue, item.Huid, null);
        var created = await first.CreateAsync(request, default); Assert.Equal(51, created.Id);
        var restarted = new SampleItemRepository(environment, options);
        Assert.Null(await restarted.GetItemAsync(created.Id, default));
        Assert.Equal(52, (await restarted.CreateAsync(request, default)).Id);
        await File.WriteAllTextAsync(Path.Combine(folder, "sample-item-last-id.json"), "invalid");
        await Assert.ThrowsAsync<System.Text.Json.JsonException>(() => restarted.CreateAsync(request with { Code = "SAMPLE-NEXT" }, default));
    }
}
