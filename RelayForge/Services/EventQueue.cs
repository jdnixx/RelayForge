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
                                      ORDER BY CreatedAtUtc
                                      LIMIT 1
                                  )
                                  AND Status = 'Pending'
                                  RETURNING *;
                                  """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return claimedEvents.SingleOrDefault();
    }
}