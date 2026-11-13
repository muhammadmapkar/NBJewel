// Role: Exercises the real existing Python cutout script through protected ERP item uploads,
// verifies actual PNG alpha and durable item links across fresh API hosts, without SQL.
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NbJewel.Api.Tests.Jewelry;

public sealed class ItemCutoutHttpTests
{
    // Synthetic gold square on white, generated as a test fixture rather than a product photo.
    private static readonly byte[] Photo = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAEAAAABACAIAAAAlC+aJAAAAlklEQVR4nO3awQmEUAAD0XGwDyuwMCuyMCuwkm8NIosM67sHArlmGmNQJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3ESJ3Hz3cCxL/zSup3/tYDESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESZzESdz0/YVeJnESJ3ESJ3ESJ3ESJ3ESJ3ESJ3G+XeCpCyqgCXui4j6ZAAAAAElFTkSuQmCC");
    private static string Storage() => Path.Combine(Path.GetTempPath(), "nbjewel-item-cutout-" + Guid.NewGuid().ToString("N"));
    private static string Python => Environment.GetEnvironmentVariable("CUTOUT_PYTHON_PATH") ?? "python3";
    private static KnowledgeHttpFactory Factory(string storage) => new()
    {
        TestConfiguration = new() { ["ITEM_ASSET_STORAGE_PATH"] = storage, ["CUTOUT_PYTHON_PATH"] = Python }
    };
    private static async Task<HttpClient> Admin(KnowledgeHttpFactory factory, string accountType = "erp", bool allStores = true, bool canWrite = true)
    {
        await factory.Seed(accountType, allStores, canWrite
            ? ["items.read", "items.write", "prices.read", "status.read"]
            : ["items.read", "prices.read", "status.read"]);
        var client = factory.CreateClient();
        await Csrf(client);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/login", new { email = "admin@example.test", password = "PasswordExample123!" })).StatusCode);
        await Csrf(client); return client;
    }
    private static async Task Csrf(HttpClient client)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("token").GetString());
    }
    private static async Task<HttpResponseMessage> Upload(HttpClient client, int id, byte[] photo)
    {
        using var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(photo); file.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(file, "file", "../../untrusted.exe");
        return await client.PostAsync($"/api/items/{id}/image", form);
    }

    [Fact]
    public async Task ItemUploadCreatesLinkedTransparentPreviewThatSurvivesNewApiHost()
    {
        var storage = Storage(); byte[] preview; string revision;
        await using (var first = Factory(storage))
        {
            using var client = await Admin(first);
            using var upload = await Upload(client, 7, Photo);
            Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
            var asset = await upload.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(7, asset.GetProperty("itemId").GetInt32());
            Assert.Equal("ready", asset.GetProperty("state").GetString());
            Assert.Equal("pending", asset.GetProperty("qualityReview").GetString());
            Assert.Equal("open", asset.GetProperty("licenseStatus").GetString());
            revision = asset.GetProperty("revisionId").GetString()!;
            Assert.Equal(32, revision.Length);
            Assert.Equal(64, asset.GetProperty("sourceSha256").GetString()!.Length);
            using var image = await client.GetAsync("/api/items/7/image/preview");
            Assert.Equal(HttpStatusCode.OK, image.StatusCode);
            Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
            preview = await image.Content.ReadAsByteArrayAsync();
            Assert.Equal(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, preview.Take(8));
            Assert.DoesNotContain("untrusted", asset.ToString());
            Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/items/8/image")).StatusCode);
            var publicList = await client.GetFromJsonAsync<JsonElement>("/api/storefront/products");
            Assert.Equal(0, publicList.GetProperty("totalCount").GetInt32());
        }
        await using (var restarted = Factory(storage))
        {
            using var client = await Admin(restarted);
            var asset = await client.GetFromJsonAsync<JsonElement>("/api/items/7/image");
            Assert.Equal(revision, asset.GetProperty("revisionId").GetString());
            Assert.Equal(preview, await client.GetByteArrayAsync("/api/items/7/image/preview"));
        }
        // Independent image decode verifies the file, rather than trusting the pipeline's report.
        var path = Path.Combine(storage, "revisions", revision, "preview.png");
        var info = new ProcessStartInfo(Python) { RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add("-c"); info.ArgumentList.Add("from PIL import Image; import sys; im=Image.open(sys.argv[1]); print(im.mode, im.getchannel('A').getextrema())"); info.ArgumentList.Add(path);
        using var process = Process.Start(info)!;
        var output = await process.StandardOutput.ReadToEndAsync(); var error = await process.StandardError.ReadToEndAsync(); await process.WaitForExitAsync();
        Assert.True(process.ExitCode == 0, error); Assert.Contains("RGBA (0, 255)", output);
    }

    [Fact]
    public async Task RingAndUnknownItemCannotCreateAssets()
    {
        var storage = Storage(); await using var factory = Factory(storage); using var client = await Admin(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(client, 1, Photo)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Upload(client, 9999, Photo)).StatusCode);
        Assert.False(Directory.Exists(Path.Combine(storage, "revisions")));
    }

    [Fact]
    public async Task MalformedImageIsRecordedAsFailedAndCannotServePreview()
    {
        var storage = Storage(); await using var factory = Factory(storage); using var client = await Admin(factory);
        using var response = await Upload(client, 7, JewelryTestKit.Png());
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var asset = await client.GetFromJsonAsync<JsonElement>("/api/items/7/image");
        Assert.Equal("failed", asset.GetProperty("state").GetString());
        Assert.Null(asset.GetProperty("previewSha256").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/items/7/image/preview")).StatusCode);
    }

    [Theory]
    [InlineData("erp", false, true)]
    [InlineData("customer", true, true)]
    [InlineData("erp", true, false)]
    public async Task UploadRequiresErpAllStoreScopeAndItemWritePermission(string accountType, bool allStores, bool canWrite)
    {
        var storage = Storage(); await using var factory = Factory(storage);
        using var client = await Admin(factory, accountType, allStores, canWrite);
        Assert.Equal(HttpStatusCode.Forbidden, (await Upload(client, 7, Photo)).StatusCode);
        Assert.False(Directory.Exists(Path.Combine(storage, "revisions")));
    }

    [Fact]
    public async Task SourceAndPreviewRequireErpAuthorizationAndUploadRequiresCsrf()
    {
        var storage = Storage(); await using var factory = Factory(storage); using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/items/7/image")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/items/7/image/preview")).StatusCode);
        using var client = await Admin(factory); client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        Assert.Equal(HttpStatusCode.BadRequest, (await Upload(client, 7, Photo)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync("/item-assets/revisions/source.png")).StatusCode);
    }
}
