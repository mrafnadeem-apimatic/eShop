namespace eShop.Ordering.API.Infrastructure;

using Npgsql;

#nullable enable

internal sealed class OrderingPaypalMigrationScriptHostedService(
    IHostEnvironment hostEnvironment,
    IConfiguration configuration,
    ILogger<OrderingPaypalMigrationScriptHostedService> logger) : IHostedService
{
    private const string ScriptRelativePath = "Sql\\sync_ordering_paypal_migration.sql";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("orderingdb");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'orderingdb' was not configured.");
        }

        var scriptPath = ResolveScriptPath(hostEnvironment.ContentRootPath);

        if (scriptPath is null)
        {
            throw new FileNotFoundException(
                "The Paypal migration bootstrap script was not found.",
                Path.Combine(AppContext.BaseDirectory, ScriptRelativePath));
        }

        var script = await File.ReadAllTextAsync(scriptPath, cancellationToken);

        if (string.IsNullOrWhiteSpace(script))
        {
            throw new InvalidOperationException($"The Paypal migration bootstrap script at '{scriptPath}' is empty.");
        }

        logger.LogInformation("Applying Ordering Paypal bootstrap SQL from {ScriptPath}", scriptPath);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(script, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);

        logger.LogInformation("Ordering Paypal bootstrap SQL completed successfully");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private static string? ResolveScriptPath(string contentRootPath)
    {
        var candidatePaths = new[]
        {
            Path.Combine(AppContext.BaseDirectory, ScriptRelativePath),
            Path.Combine(contentRootPath, ScriptRelativePath),
            Path.Combine(Directory.GetCurrentDirectory(), ScriptRelativePath)
        };

        return candidatePaths.FirstOrDefault(File.Exists);
    }
}