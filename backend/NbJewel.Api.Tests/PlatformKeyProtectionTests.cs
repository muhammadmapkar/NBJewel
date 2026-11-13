// Role: Verifies encrypted data protection keys survive provider restarts and reject tampering.
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NbJewel.Api.Platform;

public class PlatformKeyProtectionTests
{
    private sealed class MemoryRepository : IXmlRepository
    {
        public List<XElement> Items { get; } = [];
        public IReadOnlyCollection<XElement> GetAllElements() => Items.Select(x => new XElement(x)).ToList();
        public void StoreElement(XElement element, string friendlyName) => Items.Add(new XElement(element));
    }
    private static IDataProtectionProvider Provider(IXmlRepository storage, byte[] secret)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDataProtection().SetApplicationName("test");
        services.Configure<KeyManagementOptions>(o => o.XmlRepository = new EncryptedPlatformKeyRepository(storage, secret));
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }
    [Fact]
    public void ProvidersShareEncryptedKeysAndPurposesRemainIsolated()
    {
        var store = new MemoryRepository(); var key = new byte[32];
        var first = Provider(store, key).CreateProtector("history");
        var cipher = first.Protect("private history");
        var cookie = Provider(store, key).CreateProtector("cookies").Protect("session");
        Assert.DoesNotContain("masterKey", store.Items[0].ToString());
        var second = Provider(store, key);
        Assert.Equal("private history", second.CreateProtector("history").Unprotect(cipher));
        Assert.Equal("session", second.CreateProtector("cookies").Unprotect(cookie));
        Assert.ThrowsAny<Exception>(() => second.CreateProtector("cookies").Unprotect(cipher));
        Assert.ThrowsAny<Exception>(() => new EncryptedPlatformKeyRepository(store, Enumerable.Repeat((byte)1, 32).ToArray()).GetAllElements());
        store.Items[0].Value = Convert.ToBase64String(new byte[40]);
        Assert.ThrowsAny<Exception>(() => new EncryptedPlatformKeyRepository(store, key).GetAllElements());
    }
}
