using DocControl.Infrastructure.Data;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocControl.Api.Infrastructure;

public sealed class DatabaseMigratorHostedService : IHostedService
{
    private readonly DatabaseInitializer initializer;
    private readonly ILogger<DatabaseMigratorHostedService> logger;

    public DatabaseMigratorHostedService(DatabaseInitializer initializer, ILogger<DatabaseMigratorHostedService> logger)
    {
        this.initializer = initializer;
        this.logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Ensuring database schema exists...");
        await initializer.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
        logger.LogInformation("Database schema ensured.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
