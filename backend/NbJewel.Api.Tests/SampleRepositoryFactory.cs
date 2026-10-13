// Role: Test helper that builds a fresh SampleItemRepository from the real
// sample-data folder, so each test starts from the same data and changes
// made in one test never leak into another. CreateWithRates builds one whose
// metal rates come from a test-only fixture file (for example the poisoned file,
// whose 22K gold rate is the deliberately wrong value of 1.00, used by the grounding
// test). The demo data itself uses a realistic 22K rate of 66.00.
// DataFolder gives the same data to the SQL tests, so both providers load identical files.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Tests;

internal static class SampleRepositoryFactory
{
    public static SampleItemRepository Create() => new(new TestEnvironment(FindRoot()));

    public static SampleItemRepository CreateWithPoisonedRates() => CreateWithRates("metal-rates.poisoned.json");

    public static SampleItemRepository CreateWithRates(string fixtureFile) => new(new TestEnvironment(ContentRootWithRates(fixtureFile)));

    // The repository root: the folder that holds sample-data and docs.
    public static string FindRoot()
    {
        var path = Directory.GetCurrentDirectory();
        while (path is not null && !File.Exists(Path.Combine(path, "sample-data", "items.json"))) path = Directory.GetParent(path)?.FullName;
        return path ?? throw new DirectoryNotFoundException("sample-data was not found.");
    }

    // A sample-data folder path. With a fixture file, it is a temp copy whose metal-rates.json is swapped for that fixture.
    public static string DataFolder(string? ratesFixture = null) => Path.Combine(ratesFixture is null ? FindRoot() : ContentRootWithRates(ratesFixture), "sample-data");

    private static string ContentRootWithRates(string fixtureFile)
    {
        var folder = Path.Combine(Path.GetTempPath(), "nbjewel-fixture-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(folder, "sample-data");
        Directory.CreateDirectory(data);
        foreach (var file in Directory.GetFiles(Path.Combine(FindRoot(), "sample-data"), "*.json"))
            File.Copy(file, Path.Combine(data, Path.GetFileName(file)));
        File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures", fixtureFile), Path.Combine(data, "metal-rates.json"), overwrite: true);
        return folder;
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
