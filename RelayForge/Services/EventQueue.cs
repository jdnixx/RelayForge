using Microsoft.EntityFrameworkCore;
using RelayForge.Data;
using RelayForge.Models;

namespace RelayForge.Services;

public class EventQueue
{
    private readonly RelayForgeDbContext  _dbContext;

    public EventQueue(RelayForgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Event?> GetNextPendingEventAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var claimedEvents = await _dbContext.Events
            .FromSqlInterpolated($"""
                                  UPDATE Events
                                  SET Status = 'Processing',
                                      Attempts = Attempts + 1,
                                      LastAttemptAtUtc = {now}
                                  WHERE Id = (
                                      SELECT Id
                                      FROM Events
                                      WHERE Status = 'Pending'
                                      OR (Status = 'Failed' AND NextAttemptAtUtc <= {now})
                                      ORDER BY CreatedAtUtc
                                      LIMIT 1
                                  )
                                  AND (Status = 'Pending'
                                    OR (Status = 'Failed' AND NextAttemptAtUtc <= {now})
                                  )
                                  RETURNING *;
                                  """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return claimedEvents.SingleOrDefault();
    }

    public async Task MarkAsDeliveredAsync(Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var deliveryEvent = await _dbContext.Events
            .FirstOrDefaultAsync(e=> e.Id == eventId, cancellationToken);

        if (deliveryEvent == null) return;

        deliveryEvent.Status = "Delivered";

        await _dbContext.SaveChangesAsync();
    }
    
    public async Task MarkAsFailedAsync(Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var failedEvent = await _dbContext.Events
            .FirstOrDefaultAsync(e=> e.Id == eventId, cancellationToken);
        
        if (failedEvent == null) return;
        
        if (failedEvent.Attempts >= 5)
        {
            failedEvent.Status = "PermanentlyFailed";
        }
        else
        {
            var exponentialDelaySeconds = 5 * Math.Pow(2, failedEvent.Attempts-1);
            var cappedDelaySeconds = Math.Min(exponentialDelaySeconds, 3600);
    
            var jitterMultiplier = Random.Shared.NextDouble() * 0.4 + 0.8;
            var delay = TimeSpan.FromSeconds(cappedDelaySeconds * jitterMultiplier);
    
            failedEvent.Status = "Failed";
            failedEvent.NextAttemptAtUtc =  DateTime.UtcNow.Add(delay);
        }
        
        

        await _dbContext.SaveChangesAsync();
    }
}