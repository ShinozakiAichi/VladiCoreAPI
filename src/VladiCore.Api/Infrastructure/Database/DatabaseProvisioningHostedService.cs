using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using VladiCore.Data.Provisioning;

namespace VladiCore.Api.Infrastructure.Database;

public sealed class DatabaseProvisioningHostedService : IHostedService
{
    private readonly ILogger<DatabaseProvisioningHostedService> _logger;
    private readonly ISchemaBootstrapper _bootstrapper;

    public DatabaseProvisioningHostedService(
        ILogger<DatabaseProvisioningHostedService> logger,
        ISchemaBootstrapper bootstrapper)
    {
        _logger = logger;
        _bootstrapper = bootstrapper;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _bootstrapper.RunAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Database provisioning failed.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
