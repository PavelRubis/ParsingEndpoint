using System.Reflection;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using ParsingEndpoint.Database;
using ParsingEndpoint.Models;
using ParsingEndpoint.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.WriteIndented = true;
});

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var message = context.ModelState.Values.SelectMany(value => value.Errors)
            .Select(error => error.ErrorMessage).FirstOrDefault() ?? "Invalid JSON request body.";
        return new BadRequestObjectResult(ParsingResponse.Failure("INVALID_JSON", message));
    };
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

builder.Services.AddScoped<IDapperSession>(_ => new DapperSession(builder.Configuration.GetConnectionString("Postgres") ?? throw new InvalidOperationException("ConnectionStrings:Postgres is required.")));
builder.Services.AddScoped<IElementRepository, ElementRepository>();
builder.Services.AddScoped<IParsingService, ParsingService>();

var app = builder.Build();

app.UseSwagger(options => options.RouteTemplate = "api/swagger/{documentName}/swagger.json");
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api/swagger";
    options.SwaggerEndpoint("/api/swagger/v1/swagger.json", "ParsingEndpoint v1");
});
app.MapControllers();
app.Run();

public partial class Program;
