// Role: Persists data protection keys in SQL with authenticated encryption under a backend secret.
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Data.SqlClient;

namespace NbJewel.Api.Platform;

public static class PlatformKeyProtection
{
    public static IServiceCollection AddPlatformKeyProtection(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var value = configuration["AI_DATA_PROTECTION_SECRET"];
        var connection = configuration["AI_PLATFORM_CONNECTION"] ?? configuration["NBJEWEL_IDENTITY_CONNECTION"];
        if (value is null)
        {
            if (!environment.IsDevelopment()) throw new InvalidOperationException("AI_DATA_PROTECTION_SECRET is required outside Development.");
            services.AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider());
            Console.Error.WriteLine("Warning: data protection is ephemeral and is not restart durable.");
            return services;
        }
        byte[] secret;
        try { secret = Convert.FromBase64String(value); }
        catch (FormatException) { throw new InvalidOperationException("AI_DATA_PROTECTION_SECRET must be a base64-encoded 32-byte value."); }
        if (secret.Length != 32) throw new InvalidOperationException("AI_DATA_PROTECTION_SECRET must be a base64-encoded 32-byte value.");
        if (string.IsNullOrWhiteSpace(connection)) throw new InvalidOperationException("A platform SQL connection is required for data protection.");
        var repository = new EncryptedPlatformKeyRepository(new SqlPlatformKeyRepository(connection), secret);
        // Read eagerly so an inaccessible or unreadable existing ring fails startup.
        repository.GetAllElements();
        services.AddDataProtection().SetApplicationName("NbJewel.Platform");
        services.Configure<KeyManagementOptions>(options => options.XmlRepository = repository);
        return services;
    }
}

public sealed class EncryptedPlatformKeyRepository(IXmlRepository inner, byte[] secret) : IXmlRepository
{
    private readonly byte[] key = secret.Length == 32 ? (byte[])secret.Clone() : throw new ArgumentException("A 32-byte key is required.", nameof(secret));
    public IReadOnlyCollection<XElement> GetAllElements() => inner.GetAllElements().Select(Decrypt).ToList();
    public void StoreElement(XElement element, string friendlyName)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12), plain = Encoding.UTF8.GetBytes(element.ToString(SaveOptions.DisableFormatting));
        byte[] cipher = new byte[plain.Length], tag = new byte[16];
        using var aes = new AesGcm(key, 16);
        aes.Encrypt(nonce, plain, cipher, tag);
        inner.StoreElement(new XElement("encryptedKey", Convert.ToBase64String(nonce.Concat(tag).Concat(cipher).ToArray())), friendlyName);
        CryptographicOperations.ZeroMemory(plain);
    }
    private XElement Decrypt(XElement element)
    {
        if (element.Name != "encryptedKey") throw new CryptographicException("Invalid protected key record.");
        try
        {
            var data = Convert.FromBase64String(element.Value);
            if (data.Length < 29) throw new CryptographicException("Invalid protected key record.");
            byte[] plain = new byte[data.Length - 28];
            using var aes = new AesGcm(key, 16);
            aes.Decrypt(data.AsSpan(0, 12), data.AsSpan(28), data.AsSpan(12, 16), plain);
            try { return XElement.Parse(Encoding.UTF8.GetString(plain)); }
            finally { CryptographicOperations.ZeroMemory(plain); }
        }
        catch (Exception ex) when (ex is FormatException or System.Xml.XmlException or ArgumentException)
        { throw new CryptographicException("Invalid protected key record.", ex); }
    }
}

internal sealed class SqlPlatformKeyRepository(string connectionString) : IXmlRepository
{
    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand("SELECT [ProtectedXml] FROM [AiDataProtectionKeys] ORDER BY [Id]", connection);
        using var reader = command.ExecuteReader();
        var elements = new List<XElement>();
        while (reader.Read()) elements.Add(XElement.Parse(reader.GetString(0)));
        return elements;
    }
    public void StoreElement(XElement element, string friendlyName)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand("INSERT INTO [AiDataProtectionKeys] ([FriendlyName], [ProtectedXml]) VALUES (@name, @xml)", connection);
        command.Parameters.Add(new SqlParameter("@name", System.Data.SqlDbType.NVarChar, 256) { Value = friendlyName });
        command.Parameters.Add(new SqlParameter("@xml", System.Data.SqlDbType.NVarChar, -1) { Value = element.ToString(SaveOptions.DisableFormatting) });
        command.ExecuteNonQuery();
    }
}
