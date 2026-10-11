// Role: Test helper that builds a fresh SampleItemRepository from the real
// sample-data folder, so each test starts from the same data and changes
// made in one test never leak into another. CreateWithPoisonedRates builds one
// whose 22K gold rate is the deliberately wrong test-only value of 1.00, for the
// grounding test. The demo data itself uses a realistic 22K rate of 66.00.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Tests;

internal static class SampleRepositoryFactory
{
    public static SampleItemRepository Create() => new(new TestEnvironment(FindSampleRoot()));

    // Copies sample-data to a temp folder and swaps in the test-only metal rates.
    public static SampleItemRepository CreateWithPoisonedRates()
    {
        var folder = Path.Combine(Path.GetTempPath(), "nbjewel-poisoned-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(folder, "sample-data");
        Directory.CreateDirectory(data);
        foreach (var file in Directory.GetFiles(Path.Combine(FindSampleRoot(), "sample-data"), "*.json"))
            File.Copy(file, Path.Combine(data, Path.GetFileName(file)));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", "metal-rates.poisoned.json"), Path.Combine(data, "metal-rates.json"), overwrite: true);
        return new SampleItemRepository(new TestEnvironment(folder));
    }

    private static string FindSampleRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (path is not null && !File.Exists(Path.Combine(path, "sample-data", "items.json"))) path = Directory.GetParent(path)?.FullName;
        return path ?? throw new DirectoryNotFoundException("sample-data was not found.");
    }

    private sealed class TestEnvironment(string contentRoot) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "NbJewel.Api.Tests";
        public string EnvironmentName { get; set; } = "Testing";
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRoot;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
