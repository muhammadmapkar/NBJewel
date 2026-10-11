// Role: Composes the API, registers the sample or SQL repository, configures Swagger,
// enables the Angular development origin through CORS, and sets up the client
// that forwards chat questions to the Python AI service.

using NbJewel.Api.Repositories;

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
var provider = builder.Configuration["Repository:Provider"] ?? "Sample";
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
builder.Services.AddHttpClient(NbJewel.Api.Controllers.ChatController.ClientName, client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI_SERVICE_URL"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromSeconds(90); // Gemini can take 30 seconds or more per step, and a question needs several steps
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AngularDevelopment");
app.MapControllers();
app.Run();

public partial class Program;
