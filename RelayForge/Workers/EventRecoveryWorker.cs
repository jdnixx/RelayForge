using RelayForge.Services;

namespace RelayForge.Workers;

public class EventRecoveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EventRecoveryWorker> _logger;
    
    public EventRecoveryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger <EventRecoveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event recovery worker started.");
        
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = _scopeFactory.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();
            await queue.RecoverStaleEventAsync(stoppingToken);
        }
    }
}
        
