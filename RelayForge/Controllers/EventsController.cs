using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using RelayForge.Data;
using RelayForge.Models;

namespace RelayForge.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly RelayForgeDbContext _dbContext;

    public EventsController(RelayForgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpPost]
    public async Task<IActionResult> Post(
        [FromBody] CreateEventRequest request,
        CancellationToken  cancellationToken)
    {
        Event newEvent = new Event()
        {
            Id = Guid.NewGuid(),
            CreatedAtUtc = DateTime.UtcNow,
            Status = "Pending",
            DestinationUrl = request.DestinationUrl,
            PayloadJson = JsonSerializer.Serialize(request.Payload)
        };
        
        _dbContext.Events.Add(newEvent);
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        return Accepted(new
        {
            newEvent.Id,
            newEvent.Status
        });
    }
}