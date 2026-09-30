// Role: Tests price calculation against the synthetic Zork Ring fixture and
// verifies the 22K grounding rate and percentage making charge.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Tests;

public sealed class PriceCalculationTests
{
    [Fact]
    public async Task ZorkRing_UsesLatest22KRateAndPercentMakingCharge()
    {
        var repository = new SampleItemRepository(new TestEnvironment());
        var price = await repository.GetPriceAsync(1, CancellationToken.None);

        Assert.NotNull(price);
        Assert.Equal(1m, price.RatePerGram);
        Assert.Equal(42.758m, price.NetWeight);
        Assert.Equal(2.81m, price.MakingCharge);
        Assert.Equal(45.57m, price.TotalPrice);
    }

    private sealed class TestEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "NbJewel.Api.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = FindRoot();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        private static string FindRoot()
        {
            var path = Directory.GetCurrentDirectory();
            while (path is not null && !File.Exists(Path.Combine(path, "sample-data", "items.json"))) path = Directory.GetParent(path)?.FullName;
            return path ?? throw new DirectoryNotFoundException("sample-data was not found.");
        }
    }
}
