using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AssignmentPRN.DataAccess.Services;

public sealed class DatabaseInitializerHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializerHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>();

        try
        {
            await initializer.InitializeAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Startup must not fail because seeding did: the app still serves the login page.
            logger.LogError(exception, "Khởi tạo dữ liệu mẫu thất bại.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
