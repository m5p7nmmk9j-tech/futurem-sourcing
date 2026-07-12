namespace Futurem.Sourcing.Api.Services;

public sealed class ContainerReservationExpiryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ContainerReservationExpiryWorker> _logger;

    public ContainerReservationExpiryWorker(
        IServiceScopeFactory scopeFactory,
        TimeProvider timeProvider,
        ILogger<ContainerReservationExpiryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await ExpireOnceAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(15), _timeProvider);
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await ExpireOnceAsync(stoppingToken);
    }

    private async Task ExpireOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ContainerReservationService>();
            var count = await service.ExpireAsync(_timeProvider.GetUtcNow().UtcDateTime);
            if (count > 0)
                _logger.LogInformation("Expired {ReservationCount} container inventory reservations", count);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to expire container inventory reservations");
        }
    }
}
