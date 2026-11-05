using System.Text;
using RelayForge.Services;

namespace RelayForge.Workers;

public class DeliveryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<DeliveryWorker> _logger;

    public DeliveryWorker(
        IServiceScopeFactory scopeFactory,
        HttpClient httpClient,
        ILogger <DeliveryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _httpClient = httpClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Delivery worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();

            var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

            var nextEvent = await queue.GetNextPendingEventAsync(stoppingToken);

            if (nextEvent is null)
            {
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
                continue;
            }
            
            //_logger.LogInformation("Claimed event {EventId} for delivery.", nextEvent.Id);

            try
            {
                using var content = new StringContent(nextEvent.PayloadJson,
                    Encoding.UTF8, "application/json");
                
                var response  = await _httpClient.PostAsync(nextEvent.DestinationUrl,
                    content,  stoppingToken);
                
                response.EnsureSuccessStatusCode();
                
                await queue.MarkAsDeliveredAsync(nextEvent.Id,  stoppingToken);
                
                _logger.LogInformation("Successfully delivered event {EventId}.",  nextEvent.Id);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error during delivery of event {EventId}.", nextEvent.Id);
                
                await queue.MarkAsFailedAsync(nextEvent.Id,  stoppingToken);
            }
        }
    }
}