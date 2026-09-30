// Role: Composes the API, registers the sample repository, configures Swagger,
// and enables the Angular development origin through CORS.

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

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AngularDevelopment");
app.MapControllers();
app.Run();

public partial class Program;
