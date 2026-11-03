using Microsoft.Extensions.DependencyInjection;
using RelayForge.Data;
using RelayForge.Models;
using RelayForge.Services;

namespace RelayForge.Tests;

public class EventQueueTests
{
    [Fact]
    public async Task GetNextPendingEvent_ReturnsOldestPendingEvent()
    {
        // Arrange
        using var factory = new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

        var olderEvent = new Event()
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Status = "Pending"
        };
        
        var newerEvent = new Event()
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            Status = "Pending"
        };
        
        db.Events.AddRange(olderEvent, newerEvent);
        await db.SaveChangesAsync();
        
        // Act
        var result = await queue.GetNextPendingEventAsync();
        
        // Assert
        Assert.NotNull(result);
        Assert.Equal(olderEvent.Id, result.Id);
        Assert.Equal("Processing", result.Status);
        Assert.Equal(1, result.Attempts);
        Assert.NotNull(result.LastAttemptAtUtc);
    }

    [Fact]
    public async Task GetNextPendingEvent_ConcurrentCalls_DoNotClaimSameEvent()
    {
        //Arrange
        using var factory = new RelayForgeFactory();

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();

            db.Events.Add(new Event()
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                Status = "Pending"
            });

            await db.SaveChangesAsync();
        }

        var tasks = Enumerable.Range(0, 2).Select(async _ =>
        {
            using var scope = factory.Services.CreateScope();
            var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

            return await queue.GetNextPendingEventAsync();
        });

        var results = await Task.WhenAll(tasks);
        
        // Assert
        Assert.Single(results.Where(e => e is not null));
    }
}