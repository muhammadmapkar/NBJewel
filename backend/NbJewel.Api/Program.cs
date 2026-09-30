// Role: Composes the API, registers the sample repository, configures Swagger,
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
builder.Services.AddSingleton<IItemRepository, SampleItemRepository>();
builder.Services.AddHttpClient(NbJewel.Api.Controllers.ChatController.ClientName, client =>
{
    client.BaseAddress = new Uri(builder.Configuration["AI_SERVICE_URL"] ?? "http://localhost:8000");
    client.Timeout = TimeSpan.FromSeconds(30);
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AngularDevelopment");
app.MapControllers();
app.Run();

public partial class Program;
