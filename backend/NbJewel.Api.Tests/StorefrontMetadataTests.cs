// Role: Exercises storefront metadata through real catalog writes and reads, plus the labelled
// sample platinum fixture. The same tests can cover SQL only in a separately authorized SQL run.

using System.Text.Json;
using System.Text.Json.Nodes;
using NbJewel.Api.Models;

namespace NbJewel.Api.Tests;

public sealed class StorefrontMetadataTests
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task PlatinumSample_KeepsMetalPurityAndLabelledMetadataWhenReadAndFiltered(string provider)
    {
        var repository = Repos.Create(provider);
        var result = await repository.SearchItemsAsync(null, null, "platinum", "950", null, 1, 100, Ct);
        var item = Assert.Single(result.Items);
        Assert.Equal(1, result.TotalCount);
        Assert.Equal("SAMPLE-PT-0031", item.Code);
        Assert.Equal("Sample platinum necklace", item.Name);
        Assert.Equal("Platinum", item.MetalType);
        Assert.Equal("950", item.Purity);
        Assert.Equal("Draft", item.Status);
        Assert.Null(item.ImageUrl); // No unreviewed cutout or substitute product photo is claimed.
        var read = (await repository.GetItemAsync(item.Id, Ct))!;
        var metadata = Metadata(read);
        Assert.True(metadata["isSample"]!.GetValue<bool>());
        Assert.Contains("sample", metadata["description"]!.GetValue<string>(), StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Sample platinum", metadata["collection"]!.GetValue<string>());
        Assert.Equal("2026-09-30", metadata["addedOn"]!.GetValue<string>());
        var rate = Assert.Single(await repository.GetLatestRatesAsync(Ct), x => x.MetalType == "Platinum");
        Assert.Equal("950", rate.Purity);
        Assert.Equal(110m, rate.RatePerGram);
        Assert.Equal(new DateOnly(2026, 9, 30), rate.EffectiveDate);
        Assert.Equal(110m, (await repository.GetPriceAsync(item.Id, Ct))!.RatePerGram);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task Create_ReadAndSearchRetainStorefrontFieldsWithoutChangingErpFields(string provider)
    {
        var repository = Repos.Create(provider);
        var created = await repository.CreateAsync(Request().Deserialize<ItemCreateRequest>(Web)!, Ct);
        var read = (await repository.GetItemAsync(created.Id, Ct))!;
        Assert.Equal("Draft", read.Status);
        Assert.Equal("Gold", read.MetalType);
        Assert.Equal("22K", read.Purity);
        Assert.Equal(8.5m, read.NetWt);
        var metadata = Metadata(read);
        Assert.Equal("Sample display note", metadata["note"]!.GetValue<string>());
        Assert.Equal("Sample necklace description", metadata["description"]!.GetValue<string>());
        Assert.Equal("Everyday sample", metadata["collection"]!.GetValue<string>());
        Assert.Equal(new[] { "Daily wear", "Gifting" }, metadata["occasions"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal(new[] { "Sample zircon" }, metadata["stones"]!.AsArray().Select(x => x!.GetValue<string>()));
        Assert.Equal("Adjustable", metadata["size"]!.GetValue<string>());
        Assert.Equal("new", metadata["badge"]!.GetValue<string>());
        Assert.Equal(3, metadata["popularity"]!.GetValue<int>());
        Assert.Equal("2026-09-30", metadata["addedOn"]!.GetValue<string>());
        Assert.True(metadata["isSample"]!.GetValue<bool>());
        var found = Assert.Single((await repository.SearchItemsAsync("SF-TEST", null, null, null, null, 1, 100, Ct)).Items);
        Assert.True(JsonNode.DeepEquals(metadata, Metadata(found)));
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task ExplicitMetadataUpdateReplacesTheOldMetadata(string provider)
    {
        var repository = Repos.Create(provider);
        var payload = Request();
        var created = await repository.CreateAsync(payload.Deserialize<ItemCreateRequest>(Web)!, Ct);
        payload["name"] = "Changed ERP name";
        payload["storefront"]!["description"] = "Revised sample description";
        payload["storefront"]!["occasions"] = new JsonArray("Wedding");
        payload["storefront"]!["badge"] = null;
        await repository.UpdateAsync(created.Id, payload.Deserialize<ItemUpdateRequest>(Web)!, Ct);
        var read = (await repository.GetItemAsync(created.Id, Ct))!;
        Assert.Equal("Changed ERP name", read.Name);
        Assert.Equal("Draft", read.Status);
        var metadata = Metadata(read);
        Assert.Equal("Revised sample description", metadata["description"]!.GetValue<string>());
        Assert.Equal("Wedding", Assert.Single(metadata["occasions"]!.AsArray())!.GetValue<string>());
        Assert.Null(metadata["badge"]);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task LegacyUpdateOmittingMetadataPreservesExistingStorefrontMetadata(string provider)
    {
        var repository = Repos.Create(provider);
        var payload = Request();
        var created = await repository.CreateAsync(payload.Deserialize<ItemCreateRequest>(Web)!, Ct);
        payload.Remove("storefront");
        payload["name"] = "Legacy ERP edit";
        await repository.UpdateAsync(created.Id, payload.Deserialize<ItemUpdateRequest>(Web)!, Ct);
        var read = (await repository.GetItemAsync(created.Id, Ct))!;
        Assert.Equal("Legacy ERP edit", read.Name);
        Assert.Equal("Sample necklace description", Metadata(read)["description"]!.GetValue<string>());
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task InvalidMetadataIsRejectedBeforeItCanCreateOrOverwriteAnItem(string provider)
    {
        var repository = Repos.Create(provider);
        var payload = Request();
        var created = await repository.CreateAsync(payload.Deserialize<ItemCreateRequest>(Web)!, Ct);
        var invalid = new (string Key, JsonNode? Value)[]
        {
            ("description", JsonValue.Create(new string('x', 4001))),
            ("description", JsonValue.Create(" ")),
            ("collection", JsonValue.Create(new string('x', 101))),
            ("badge", JsonValue.Create("unreviewed-award")),
            ("popularity", JsonValue.Create(-1)),
            ("occasions", null),
            ("occasions", new JsonArray(JsonValue.Create(new string('x', 101)))),
            ("stones", new JsonArray(Enumerable.Range(0, 17).Select(x => (JsonNode?)JsonValue.Create("stone")).ToArray()))
        };
        foreach (var (key, value) in invalid)
        {
            var bad = Request();
            bad["storefront"]![key] = value;
            bad["code"] = "SF-INVALID";
            await Assert.ThrowsAsync<DomainValidationException>(() => repository.CreateAsync(bad.Deserialize<ItemCreateRequest>(Web)!, Ct));
            bad["code"] = "SF-TEST";
            bad["name"] = "Must not overwrite";
            await Assert.ThrowsAsync<DomainValidationException>(() => repository.UpdateAsync(created.Id, bad.Deserialize<ItemUpdateRequest>(Web)!, Ct));
        }
        var read = (await repository.GetItemAsync(created.Id, Ct))!;
        Assert.Equal("Sample ERP necklace", read.Name);
        Assert.Equal("Sample necklace description", Metadata(read)["description"]!.GetValue<string>());
        Assert.Empty((await repository.SearchItemsAsync("SF-INVALID", null, null, null, null, 1, 100, Ct)).Items);
    }

    [Theory]
    [MemberData(nameof(Repos.Providers), MemberType = typeof(Repos))]
    public async Task LegacyItemsRemainWithoutStorefrontMetadata(string provider)
    {
        var repository = Repos.Create(provider);
        var legacy = Request();
        legacy.Remove("storefront");
        var item = await repository.CreateAsync(legacy.Deserialize<ItemCreateRequest>(Web)!, Ct);
        Assert.Null(JsonSerializer.SerializeToNode((await repository.GetItemAsync(item.Id, Ct))!, Web)!["storefront"]);
    }

    private static JsonObject Metadata(Item item) =>
        Assert.IsType<JsonObject>(JsonSerializer.SerializeToNode(item, Web)!["storefront"]);

    private static JsonObject Request() => JsonNode.Parse("""
        {
          "code": "SF-TEST", "name": "Sample ERP necklace", "categoryId": 3, "designId": 6,
          "metalType": "Gold", "purity": "22K", "grossWt": 10, "netWt": 999, "stoneWt": 1.5,
          "makingType": "percent", "makingValue": 8, "huid": null, "imageUrl": null,
          "storefront": {
            "note": "Sample display note", "description": "Sample necklace description",
            "collection": "Everyday sample", "occasions": ["Daily wear", "Gifting"],
            "stones": ["Sample zircon"], "size": "Adjustable", "badge": "new",
            "popularity": 3, "addedOn": "2026-09-30", "isSample": true
          }
        }
        """)!.AsObject();
}
