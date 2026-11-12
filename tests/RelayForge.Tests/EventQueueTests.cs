using Microsoft.EntityFrameworkCore;
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

    [Fact]
    public async Task MarkAsDelivered_ChangesEventStatus()
    {
        using var factory = new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

        var newEvent = new Event()
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            Status = "Processing"
        };
        
        db.Events.AddRange(newEvent);
        await db.SaveChangesAsync();

        await queue.MarkAsDeliveredAsync(newEvent.Id);
        
        Assert.Equal("Delivered", newEvent.Status);
    }
    
    [Fact]
    public async Task MarkAsFailed_ChangesEventStatus()
    {
        using var factory = new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

        var newEvent = new Event
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            Status = "Processing",
            Attempts = 1
        };

        db.Events.Add(newEvent);
        await db.SaveChangesAsync();

        await queue.MarkAsFailedAsync(newEvent.Id);

        Assert.Equal("Failed", newEvent.Status);
        Assert.NotNull(newEvent.NextAttemptAtUtc);
        Assert.True(newEvent.NextAttemptAtUtc >  DateTime.UtcNow);
    }
    
    [Fact]
    public async Task GetNextPendingEvent_DoesNotClaimFailedEventBeforeRetryTime()
    {
        using var factory = new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

        db.Events.Add(new Event
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Status = "Failed",
            Attempts = 1,
            NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(5)
        });

        await db.SaveChangesAsync();

        var result = await queue.GetNextPendingEventAsync();

        Assert.Null(result);
    }
    
    [Fact]
    public async Task GetNextPendingEvent_ClaimsFailedEventAfterRetryTime()
    {
        using var factory = new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

        var failedEvent = new Event
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow.AddMinutes(-10),
            Status = "Failed",
            Attempts = 1,
            NextAttemptAtUtc = DateTime.UtcNow.AddMinutes(-5)
        };

        db.Events.Add(failedEvent);
        await db.SaveChangesAsync();

        var result = await queue.GetNextPendingEventAsync();

        Assert.NotNull(result);
        Assert.Equal(failedEvent.Id, result.Id);
        Assert.Equal("Processing", result.Status);
        Assert.Equal(2, result.Attempts);
    }
    
    [Fact]
    public async Task MarkAsFailed_PermanentlyFailsEventAfterMaxAttempts()
    {
        using var factory = new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

        var failedEvent = new Event
        {
            Id = Guid.NewGuid(),
            Status = "Processing",
            Attempts = 5
        };

        db.Events.Add(failedEvent);
        await db.SaveChangesAsync();

        await queue.MarkAsFailedAsync(failedEvent.Id);

        Assert.Equal("PermanentlyFailed", failedEvent.Status);
        Assert.Null(failedEvent.NextAttemptAtUtc);
    }
    
    [Fact]
    public async Task MarkAsFailed_SchedulesRetryBeforeMaxAttempts()
    {
        using var factory = new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();

        var failedEvent = new Event
        {
            Id = Guid.NewGuid(),
            Status = "Processing",
            Attempts = 4
        };

        db.Events.Add(failedEvent);
        await db.SaveChangesAsync();

        var before = DateTime.UtcNow;

        await queue.MarkAsFailedAsync(failedEvent.Id);

        var after = DateTime.UtcNow;

        Assert.Equal("Failed", failedEvent.Status);
        Assert.NotNull(failedEvent.NextAttemptAtUtc);
        Assert.True(failedEvent.NextAttemptAtUtc > before);
        Assert.True(failedEvent.NextAttemptAtUtc < after.AddHours(2));
    }
    
    [Fact]
    public async Task RecoverStaleEvents_ResetsStaleProcessingEventsToPending()
    {
        using var factory =  new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();
        
        // Arrange
        var staleEvent = new Event
        {
            Id = Guid.NewGuid(),
            Status = "Processing",
            LastAttemptAtUtc = DateTime.UtcNow.AddMinutes(-2)
        };

        await db.Events.AddAsync(staleEvent);
        await db.SaveChangesAsync();

        // Act
        await queue.RecoverStaleEventAsync();

        // Assert
        var recoveredEvent = await db.Events
            .AsNoTracking()
            .SingleAsync(e => e.Id == staleEvent.Id);

        Assert.Equal("Pending", recoveredEvent.Status);
    }
    
    [Fact]
    public async Task RecoverStaleEvents_DoesNotResetRecentProcessingEvents()
    {
        using var factory =  new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();
        
        // Arrange
        var recentEvent = new Event
        {
            Id = Guid.NewGuid(),
            Status = "Processing",
            LastAttemptAtUtc = DateTime.UtcNow.AddSeconds(-30)
        };

        await db.Events.AddAsync(recentEvent);
        await db.SaveChangesAsync();

        // Act
        await queue.RecoverStaleEventAsync();

        // Assert
        var result = await db.Events
            .AsNoTracking()
            .SingleAsync(e => e.Id == recentEvent.Id);

        Assert.Equal("Processing", result.Status);
    }
    
    [Fact]
    public async Task RecoverStaleEvents_DoesNotChangeOtherStatuses()
    {
        using var factory =  new RelayForgeFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<EventQueue>();
        
        // Arrange
        var events = new[]
        {
            new Event { Id = Guid.NewGuid(), Status = "Pending" },
            new Event { Id = Guid.NewGuid(), Status = "Failed" },
            new Event { Id = Guid.NewGuid(), Status = "Delivered" },
            new Event { Id = Guid.NewGuid(), Status = "PermanentlyFailed" }
        };

        await db.Events.AddRangeAsync(events);
        await db.SaveChangesAsync();

        // Act
        await queue.RecoverStaleEventAsync();

        // Assert
        foreach (var evt in events)
        {
            var result = await db.Events
                .AsNoTracking()
                .SingleAsync(e => e.Id == evt.Id);

            Assert.Equal(evt.Status, result.Status);
        }
    }

    [Fact]
    public async Task GetNextPendingEventAsync_ConcurrentCallsClaimUniqueEvents()
    {
        // Arrange
        // this specifically tests background services, so turn true
        using var factory = new RelayForgeFactory(true);
        using var scope = factory.Services.CreateScope();
        
        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        
        var events = Enumerable.Range(0, 20)
            .Select(_ => new Event
            {
                Id = Guid.NewGuid(),
                Status = "Pending",
                CreatedAtUtc = DateTime.UtcNow
            })
            .ToList();

        await db.Events.AddRangeAsync(events);
        await db.SaveChangesAsync();
        
        // Act
        var tasks = Enumerable.Range(0, 20)
            .Select(async _ =>
            {
                using var taskScope = factory.Services.CreateScope();
                var taskQueue = taskScope.ServiceProvider.GetRequiredService<EventQueue>();

                var claimedEvent = await taskQueue.GetNextPendingEventAsync();

                return claimedEvent?.Id;
            });

        var results = await Task.WhenAll(tasks);
        
        // Assert
        Assert.Equal(20, results.Length);
        Assert.All(results, id => Assert.NotNull(id));
        Assert.Equal(20, results.Distinct().Count());
    }
}