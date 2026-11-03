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
        var nextEvent = await _dbContext.Events
            .Where(e => e.Status == "Pending")
            .OrderBy(e => e.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (nextEvent is null) return null;
        
        nextEvent.Status = "Processing";
        nextEvent.Attempts++;
        nextEvent.LastAttemptAtUtc = DateTime.UtcNow;
        
        _dbContext.Update(nextEvent);
        
        await  _dbContext.SaveChangesAsync(cancellationToken);
        
        return nextEvent;
    }
}