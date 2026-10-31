// Role: Composes the API, registers the sample or SQL repository, configures Swagger,
// enables CORS for the origins in CORS_ORIGINS (default http://localhost:4200), sets up the
// chatbot (tools, guards, Gemini client) that runs inside this API, registers the separate
// try-on jewelry feature (images served from JEWELRY_STORAGE_PATH), and adds one global error handler.

using System.Threading.RateLimiting;
using Microsoft.Extensions.FileProviders;
using NbJewel.Api.Chat;
using NbJewel.Api.Jewelry;
using NbJewel.Api.Models;
using NbJewel.Api.Repositories;

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

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
// CORS_ORIGINS is a comma separated list, for example "https://nbjewel-demo.vercel.app".
// When it is empty, only the local Angular development server is allowed.
var corsOrigins = (builder.Configuration["CORS_ORIGINS"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(x => x.TrimEnd('/')).ToArray();
if (corsOrigins.Length == 0) corsOrigins = ["http://localhost:4200"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevelopment", policy =>
        policy.WithOrigins(corsOrigins).AllowAnyHeader().AllowAnyMethod());
});
// Repository:Provider picks the data layer. "Sample" (default) is JSON in memory.
// "Sql" uses SQL Server and needs the NBJEWEL_SQL_CONNECTION environment variable.
// An empty value (for example "Repository__Provider=" copied from .env.example) counts as not set.
var provider = string.IsNullOrWhiteSpace(builder.Configuration["Repository:Provider"]) ? "Sample" : builder.Configuration["Repository:Provider"]!;
if (provider.Equals("Sql", StringComparison.OrdinalIgnoreCase))
{
    var connectionString = builder.Configuration["NBJEWEL_SQL_CONNECTION"];
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException("Repository:Provider is Sql, but the NBJEWEL_SQL_CONNECTION environment variable is not set.");
    builder.Services.AddSingleton<IItemRepository>(new SqlItemRepository(connectionString));
}
else if (provider.Equals("Sample", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IItemRepository, SampleItemRepository>();
}
else
{
    throw new InvalidOperationException($"Unknown Repository:Provider '{provider}'. Use Sample or Sql.");
}
// The chatbot runs inside this API. The Gemini key and model come only from environment variables
// (GEMINI_API_KEY, GEMINI_MODEL, GEMINI_THINKING_LEVEL). If they are missing, /api/chat answers 503.
builder.Services.AddSingleton<IChatDataReader, ChatDataReader>();
builder.Services.AddSingleton<ChatTools>();
builder.Services.AddSingleton(GeminiOptions.FromConfiguration(builder.Configuration));
builder.Services.AddHttpClient<ILlmClient, GeminiLlmClient>(client =>
{
    client.BaseAddress = GeminiLlmClient.BaseAddress;
    client.Timeout = TimeSpan.FromSeconds(30); // one Gemini request; the whole question is capped separately in ChatController
});
builder.Services.AddScoped<ChatOrchestrator>();

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
    options.AddPolicy(OwnerPin.RateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

var app = builder.Build();
// A short owner PIN can be guessed even with the rate limit. Warn, do not stop (assumed demo gate).
var ownerPinValue = builder.Configuration["OWNER_PIN"];
if (string.IsNullOrWhiteSpace(ownerPinValue))
    app.Logger.LogWarning("OWNER_PIN is not set: the try-on owner endpoints answer 503.");
else if (ownerPinValue.Trim().Length < 8)
    app.Logger.LogWarning("OWNER_PIN is shorter than 8 characters. Use a longer one before sharing the app link.");
// One global error handler: clients get a short message, never a stack trace.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ErrorMessage("Something went wrong. Try again."));
}));
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AngularDevelopment");
app.UseRateLimiter();
// Uploaded try-on images. Only PNG and JPG are ever written there (checked by content on upload).
Directory.CreateDirectory(jewelryOptions.ImagesFolder);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(jewelryOptions.ImagesFolder),
    RequestPath = JewelryStorageOptions.ImagesRequestPath,
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Context.Response.Headers["Cache-Control"] = "public, max-age=3600";
        // The same image is loaded plainly (grid) and with CORS (camera canvas). Vary: Origin stops a
        // browser from reusing the plain cached copy, which has no CORS header, for the camera.
        context.Context.Response.Headers.Append("Vary", "Origin");
        // The try-on page draws these images on a canvas for the snapshot, so it needs CORS headers.
        context.Context.Response.Headers["Cross-Origin-Resource-Policy"] = "cross-origin";
    }
});
app.MapControllers();
app.Run();
return 0;

public partial class Program;
