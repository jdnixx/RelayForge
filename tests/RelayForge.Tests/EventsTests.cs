using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RelayForge.Data;

namespace RelayForge.Tests;

public class EventsTests
{
    [Fact]
    public async Task PostEvent_ValidRequest_ReturnsAccepted()
    {
        // Arrange
        using var factory = new RelayForgeFactory();
        using var client = factory.CreateClient();

        var request = new
        {
            destinationUrl = "http://localhost:5099/webhooks/test",
            payload = new
            {
                type = "order.created",
                orderId = "order-123"
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/events", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        var savedEvent = await db.Events.SingleOrDefaultAsync();
        
        Assert.NotNull(savedEvent);
        Assert.Equal("http://localhost:5099/webhooks/test", savedEvent.DestinationUrl);
        Assert.Equal("Pending",  savedEvent.Status);
        Assert.Contains("order-123", savedEvent.PayloadJson);
    }

    [Fact]
    public async Task PostEvent_MissingDestinationUrl_ReturnsBadRequest()
    {
        // Arrange
        using var factory = new RelayForgeFactory();
        using var client = factory.CreateClient();

        var request = new
        {
            payload = new
            {
                type = "order.created",
                orderId = "order-123"
            }
        };

        // Act
        var response = await client.PostAsJsonAsync("/api/events", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<RelayForgeDbContext>();
        
        Assert.Equal(0, await db.Events.CountAsync());
    }
}