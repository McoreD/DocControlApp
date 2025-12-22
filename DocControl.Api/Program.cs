using DocControl.Api.Infrastructure;
using DocControl.Infrastructure.Data;
using DocControl.Infrastructure.Services;
using DocControl.Core.Security;
using DocControl.AI;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Services.Configure<JsonSerializerOptions>(options =>
{
    options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddSingleton(provider =>
{
    var config = provider.GetRequiredService<IConfiguration>();
    var connStr = config.GetConnectionString("Db")
                 ?? config["DbConnection"]
                 ?? throw new InvalidOperationException("Database connection string not configured (ConnectionStrings:Db or DbConnection).");
    return new DbConnectionFactory(connStr);
});
builder.Services.AddSingleton<DatabaseInitializer>();
builder.Services.AddHostedService<DatabaseMigratorHostedService>();

builder.Services.AddSingleton<ConfigRepository>();
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<IApiKeyStore>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var path = config["ApiKeysPath"] ?? Path.Combine(AppContext.BaseDirectory, "data", "apikeys.json");
    return new JsonFileApiKeyStore(path);
});

builder.Services.AddSingleton<ProjectRepository>();

builder.Build().Run();
