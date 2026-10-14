// Role: Composes the API, registers the sample or SQL repository, configures Swagger,
// enables the Angular development origin through CORS, sets up the chatbot (tools, guards,
// Gemini client) that runs inside this API, and adds one global error handler.

using NbJewel.Api.Chat;
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AngularDevelopment", policy =>
        policy.WithOrigins("http://localhost:4200").AllowAnyHeader().AllowAnyMethod());
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

var app = builder.Build();
// One global error handler: clients get a short message, never a stack trace.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    await context.Response.WriteAsJsonAsync(new ErrorMessage("Something went wrong. Try again."));
}));
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AngularDevelopment");
app.MapControllers();
app.Run();
return 0;

public partial class Program;
