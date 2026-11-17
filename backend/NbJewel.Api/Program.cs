// Role: Composes the API, registers the sample or SQL repository, configures Swagger,
// enables CORS for the origins in CORS_ORIGINS (defaults http://localhost:4200 and http://localhost:4300), sets up the
// chatbot (tools, guards, Gemini client) that runs inside this API, registers the separate
// try-on jewelry feature (images served from JEWELRY_STORAGE_PATH), and adds one global error handler.

using System.Threading.RateLimiting;
using NbJewel.Api.Businesses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NbJewel.Api.Identity;
using NbJewel.Api.Chat;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;
using NbJewel.Api.Platform;
using NbJewel.Api.Knowledge;
using NbJewel.Api.Storefront;

// Developer tool, not the web server: "dotnet run --project backend/NbJewel.Api -- --setup-db [--reset]"
// creates the SQL Server database, applies docs/item_catalog_schema.sql, and seeds sample-data/*.json.
// --reset drops the database first. It reads NBJEWEL_SQL_CONNECTION and then exits.
if (args.Contains("--setup-db"))
{
    var connection = Environment.GetEnvironmentVariable("NBJEWEL_SQL_CONNECTION");
    if (string.IsNullOrWhiteSpace(connection)) { Console.Error.WriteLine("Set the NBJEWEL_SQL_CONNECTION environment variable first (see .env.example)."); return 1; }
    var root = Directory.GetCurrentDirectory();
    while (root is not null && !File.Exists(Path.Combine(root, "docs", "item_catalog_schema.sql"))) root = Directory.GetParent(root)?.FullName;
    if (root is null) { Console.Error.WriteLine("Run this from inside the repository so docs/item_catalog_schema.sql can be found."); return 1; }
    await SqlDatabaseSetup.RunAsync(connection, Path.Combine(root, "docs", "item_catalog_schema.sql"), Path.Combine(root, "sample-data"), reset: args.Contains("--reset"), reseed: false, Console.WriteLine);
    return 0;
}

// Prints a reviewable schema without opening a SQL connection or applying changes.
if (args.Contains("--identity-schema"))
{
    using var schema = new IdentityDatabase(new DbContextOptionsBuilder<IdentityDatabase>()
        .UseSqlServer("Server=unused;Database=unused").Options);
    Console.WriteLine(schema.Database.GenerateCreateScript());
    return 0;
}

var credentialCommand = args.Contains("--bootstrap-identity") || args.Contains("--reset-owner-password");
var privateRuntime = credentialCommand;
try
{
var builder = WebApplication.CreateBuilder(args);
privateRuntime |= PublicDemoSetup.Enabled(builder.Configuration);
if (credentialCommand && !IdentitySetup.OwnerCommandAllowed(builder.Configuration, builder.Environment))
{ Console.Error.WriteLine("Owner commands require local Development SQL mode without Sample identity or public demo."); return 1; }
PublicDemoSetup.Validate(builder.Configuration, builder.Environment);
if (args.Contains("--provision-business") && !ProvisioningCommand.Allowed(builder.Configuration, builder.Environment))
{ Console.Error.WriteLine("Business provisioning requires Development and Sample without SQL settings."); return 1; }

if (args.Contains("--platform-schema"))
{
    using var platform = new PlatformDatabase(new DbContextOptionsBuilder<PlatformDatabase>().UseSqlServer("Server=unused;Database=unused").Options);
    using var knowledge = new KnowledgeDatabase(new DbContextOptionsBuilder<KnowledgeDatabase>().UseSqlServer("Server=unused;Database=unused").Options);
    Console.WriteLine(platform.Database.GenerateCreateScript());
    Console.WriteLine(knowledge.Database.GenerateCreateScript());
    return 0;
}

// Reject unsafe explicit sample settings before any configured SQL key-ring can be opened.
if (builder.Configuration.GetValue<bool>("SampleIdentity:Enabled"))
    _ = SampleIdentityOptions.Read(builder.Configuration, builder.Environment);
builder.Services.AddPlatformKeyProtection(builder.Configuration, builder.Environment);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);
// The site and API sit behind one proxy. Forwarded headers are honored only from TRUSTED_PROXIES, so rate limits see the real client address.
TrustedProxySetup.Configure(builder.Services);

builder.AddIdentityFoundation();
builder.Services.AddSingleton<BusinessRegistry>();
builder.Services.AddScoped<BusinessMemberships>();
builder.Services.AddScoped<BusinessContext>();
builder.Services.AddScoped<ProvisioningCommand>();
builder.Services.AddHostedService<MembershipMigration>();
builder.Services.AddSingleton<IAccountVerificationSender, SmtpAccountVerificationSender>();
builder.Services.AddScoped<AccountVerification>();
builder.Services.AddScoped<CustomerPortal>();
builder.Services.AddScoped<StoreDirectory>();
builder.Services.AddHttpClient("spot-rates", client => client.MaxResponseContentBufferSize = 65536).RemoveAllLoggers();
builder.Services.AddSingleton(services => new SpotRates(services.GetRequiredService<IHttpClientFactory>().CreateClient("spot-rates"), services.GetRequiredService<IConfiguration>()));
builder.Services.AddSingleton<IPasswordWork, PasswordWork>();
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.AddService<ErpBoundaryFilter>();
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// CORS_ORIGINS is a comma separated list, for example "https://nbjewel-demo.vercel.app".
// When empty, allow only the two local Angular development origins.
var corsOrigins = (builder.Configuration["CORS_ORIGINS"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(x => x.TrimEnd('/')).ToArray();
if (corsOrigins.Length == 0) corsOrigins = ["http://localhost:4200", "http://localhost:4300"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevelopment", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials());
});
// Repository:Provider picks the data layer. "Sample" (default) is JSON in memory.
// "Sql" uses SQL Server and needs the NBJEWEL_SQL_CONNECTION environment variable.
// An empty value (for example "Repository__Provider=" copied from .env.example) counts as not set.
var provider = string.IsNullOrWhiteSpace(builder.Configuration["Repository:Provider"]) ? "Sample" : builder.Configuration["Repository:Provider"]!;
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<BusinessStorage>();
builder.Services.AddSingleton<PortalStore>();
builder.Services.AddScoped<PortalService>();
builder.Services.AddScoped<PortalPolicy>();
builder.Services.AddTransient<IItemRepository>(sp => sp.GetRequiredService<BusinessStorage>().For(BusinessStorage.Current(sp)).Items);
// Durable ERP item preview revisions use the existing Python cutout script, never a new removal engine.
builder.Services.AddSingleton(services =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    var environment = services.GetRequiredService<IWebHostEnvironment>();
    var path = configuration["ITEM_ASSET_STORAGE_PATH"];
    return new DefaultCutoutOptions(new ItemCutoutOptions(
        Path.GetFullPath(string.IsNullOrWhiteSpace(path) ? Path.Combine(environment.ContentRootPath, "App_Data", "item-assets") : path),
        configuration["CUTOUT_PYTHON_PATH"] ?? "python3",
        Path.Combine(AppContext.BaseDirectory, "scripts", "cutout-jewelry.py")));
});
builder.Services.AddTransient<ItemCutoutOptions>(sp => sp.GetRequiredService<BusinessStorage>().For(BusinessStorage.Current(sp)).Cutout);
builder.Services.AddTransient<IItemImageAssetRepository>(sp => sp.GetRequiredService<BusinessStorage>().For(BusinessStorage.Current(sp)).Assets);
builder.Services.AddTransient<ItemCutoutPipeline>(sp => sp.GetRequiredService<BusinessStorage>().For(BusinessStorage.Current(sp)).Pipeline);
builder.Services.AddSingleton(services => JewelryVisionOptions.Read(services.GetRequiredService<IConfiguration>()));
builder.Services.AddHttpClient<IJewelryVision, JewelryVisionClient>().RemoveAllLoggers();

// The chatbot runs inside this API. The Gemini key and model come only from environment variables
// (GEMINI_API_KEY, GEMINI_MODEL, GEMINI_THINKING_LEVEL). If they are missing, /api/chat answers 503.
builder.Services.AddTransient<IChatDataReader, ChatDataReader>();
builder.Services.AddTransient<ChatTools>();
builder.Services.AddSingleton(GeminiOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient<ILlmClient, GeminiLlmClient>(client =>
{
    client.BaseAddress = GeminiLlmClient.BaseAddress;
    client.Timeout = TimeSpan.FromSeconds(30); // one Gemini request; the whole question is capped separately in ChatController
});
builder.Services.AddScoped<ChatOrchestrator>();
builder.Services.AddSingleton<AssistantRegistry>();
builder.Services.AddScoped<GatewayProvider>();
var platformConnection = builder.Configuration["AI_PLATFORM_CONNECTION"] ?? builder.Configuration["NBJEWEL_IDENTITY_CONNECTION"];
builder.Services.AddDbContext<PlatformDatabase>(options => { if (!string.IsNullOrWhiteSpace(platformConnection)) options.UseSqlServer(platformConnection); });
builder.Services.AddDbContext<KnowledgeDatabase>(options => { if (!string.IsNullOrWhiteSpace(platformConnection)) options.UseSqlServer(platformConnection); });
builder.Services.AddScoped<KnowledgeDatabase>(sp => sp.GetRequiredService<BusinessStorage>().Knowledge(sp.GetRequiredService<DbContextOptions<KnowledgeDatabase>>(), BusinessStorage.Current(sp)));
// Sample product decisions persist on disk without enabling SQL or bypassing ERP identity.
if (provider.Equals("Sample", StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(platformConnection))
    builder.Services.AddTransient<IProductPublicationStore>(sp => sp.GetRequiredService<BusinessStorage>().For(BusinessStorage.Current(sp)).Publications);
builder.Services.AddScoped<KnowledgeService>();
builder.Services.AddScoped<IKnowledgeRetriever>(services => services.GetRequiredService<KnowledgeService>());
builder.Services.AddScoped<IKnowledgeEvidenceValidator>(services => services.GetRequiredService<KnowledgeService>());
builder.Services.AddScoped<IPublicationReader>(services => services.GetRequiredService<KnowledgeService>());
builder.Services.AddScoped<IAssistantTools, AssistantTools>();
builder.Services.AddScoped<AssistantAccess>();
builder.Services.AddScoped<AssistantEvidence>();
builder.Services.AddScoped<PlatformOrchestrator>();
builder.Services.AddScoped<ConversationService>();
builder.Services.AddScoped<ILegacyAssistant, LegacyAssistant>();
// Public demo startup never purges existing SQL records. Retention maintenance remains owner-managed for this demo.
if (!PublicDemoSetup.Enabled(builder.Configuration)) builder.Services.AddHostedService<PlatformMaintenance>();
builder.Services.AddScoped<CoreReadiness>();
if (PublicDemoSetup.Enabled(builder.Configuration))
    builder.Services.Configure<Microsoft.AspNetCore.Identity.SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

// Try-on jewelry: metadata JSON and images on disk under JEWELRY_STORAGE_PATH (default App_Data/jewelry
// inside the API folder). Swap FileJewelryRepository for a SQL or blob version later.
var jewelryStorage = builder.Configuration["JEWELRY_STORAGE_PATH"];
jewelryStorage = Path.GetFullPath(string.IsNullOrWhiteSpace(jewelryStorage)
    ? Path.Combine(builder.Environment.ContentRootPath, "App_Data", "jewelry")
    : jewelryStorage);
var jewelrySeed = Path.Combine(builder.Environment.ContentRootPath, "..", "..", "sample-data", "jewelry");
if (!Directory.Exists(jewelrySeed)) jewelrySeed = Path.Combine(AppContext.BaseDirectory, "sample-data", "jewelry");
var jewelryOptions = new JewelryStorageOptions(jewelryStorage, jewelrySeed);
builder.Services.AddSingleton(jewelryOptions);
builder.Services.AddSingleton<IJewelryRepository, FileJewelryRepository>();
builder.Services.AddSingleton<OwnerPin>();
// Slows down PIN guessing: at most 30 owner calls per minute per client address (assumed limit).
// Behind a hosting proxy all clients may share one address; that is fine for a one-owner demo.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("ai-ip", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions
        { PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
    options.AddPolicy(OwnerPin.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
privateRuntime |= PublicDemoSetup.Enabled(app.Configuration);
PublicDemoSetup.Validate(app.Configuration, app.Environment);
app.Services.GetRequiredService<BusinessStorage>().Validate();
app.ConfigureSampleIdentityRuntime();
if (args.Contains("--bootstrap-identity")) return await IdentitySetup.BootstrapAsync(app);
if (args.Contains("--reset-owner-password")) return await IdentitySetup.ResetOwnerPasswordAsync(app);
if (PublicDemoSetup.Enabled(app.Configuration))
{
    using var scope = app.Services.CreateScope();
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
    if (!await scope.ServiceProvider.GetRequiredService<CoreReadiness>().CheckAsync(timeout.Token))
    { Console.Error.WriteLine("Public demo core readiness failed. Check SQL schema and existing storage locally; no repair was attempted."); return 1; }
}
if (args.Contains("--provision-business"))
{
    // Assumed Sample provisioning keeps this host alive so its memory-only owner can sign in.
    await app.Services.GetServices<IHostedService>().OfType<SampleIdentityBootstrap>().Single().StartAsync(CancellationToken.None);
    await new MembershipMigration(app.Services).StartAsync(CancellationToken.None);
    using var scope = app.Services.CreateScope();
    var result = await scope.ServiceProvider.GetRequiredService<ProvisioningCommand>().RunAsync(args, Console.Out, Console.Error, CancellationToken.None);
    if (result != 0) return result;
}
// Must run first: later middleware (HTTPS redirect, rate limiter, cookies) reads the corrected address and scheme.
// It does nothing until TRUSTED_PROXIES names at least one proxy.
app.UseForwardedHeaders();
if (PublicDemoSetup.Enabled(app.Configuration)) app.Use(PublicDemoSetup.RequireHttps);
// One global error handler: clients get a short message, never a stack trace.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ErrorMessage("Something went wrong. Try again."));
}));
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseRouting();
app.UseCors("AngularDevelopment");
if (app.Environment.IsProduction()) { app.UseHsts(); if (!PublicDemoSetup.Enabled(app.Configuration)) app.UseHttpsRedirection(); }
app.UseAuthentication();
app.Use(BusinessBoundary.InvokeAsync);
app.UseAuthorization();
app.UseRateLimiter();
// Resolve published metadata on every image request, including requests for known inactive files.
app.MapGet("/jewelry-images/{file}", async (string file, IJewelryRepository repository, JewelryStorageOptions storage, HttpContext context, CancellationToken cancellationToken) =>
{
    if (Path.GetFileName(file) != file) return Results.NotFound();
    var published = await repository.ListAsync(activeOnly: true, cancellationToken);
    if (!published.Any(x => x.File == file && JewelryTypes.IsCustomerVisible(x.Type))) return Results.NotFound();
    var path = Path.Combine(storage.ImagesFolder, file);
    if (!File.Exists(path)) return Results.NotFound();
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers.CacheControl = "no-store";
    context.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
    return Results.File(path, Path.GetExtension(file).Equals(".png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg");
});
app.MapControllers();
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));
app.MapGet("/health/ready/core", async (CoreReadiness readiness, HttpContext context, CancellationToken ct) =>
{
    context.Response.Headers.CacheControl = "no-store";
    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
    timeout.CancelAfter(TimeSpan.FromSeconds(10));
    return await readiness.CheckAsync(timeout.Token) ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503);
});
app.MapGet("/health/ready", async (PlatformDatabase platform, KnowledgeDatabase knowledge, IdentityDatabase identity, GeminiOptions model, AssistantRegistry assistants, CancellationToken ct) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(model.ApiKey) || new[] { "erp", "website" }.Any(id => string.IsNullOrWhiteSpace(assistants.Get(id).Model))) return Results.StatusCode(503);
        _ = await platform.Conversations.Take(1).AnyAsync(ct);
        _ = await knowledge.Documents.Take(1).AnyAsync(ct);
        _ = await identity.Users.Take(1).AnyAsync(ct);
        return Results.Ok(new { status = "ready" });
    }
    catch (Exception) { return Results.StatusCode(503); }
});
app.Run();
return 0;
}
catch (Exception) when (privateRuntime)
{
    Console.Error.WriteLine("Private demo command failed. Check configuration, SQL schema and storage locally. No secret details are logged.");
    return 1;
}

public partial class Program;
