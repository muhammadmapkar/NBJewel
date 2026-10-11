// Role: Test helper that builds a fresh SampleItemRepository from the real
// sample-data folder, so each test starts from the same data and changes
// made in one test never leak into another.

using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NbJewel.Api.Repositories;

namespace NbJewel.Api.Tests;

internal static class SampleRepositoryFactory
{
    public static SampleItemRepository Create() => new(new TestEnvironment());

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
