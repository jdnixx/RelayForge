using System.ComponentModel.DataAnnotations;

namespace RelayForge.Models;

public class CreateEventRequest
{
    [Required]
    [Url]
    public string DestinationUrl { get; set; } = string.Empty;
    public PayloadRequest Payload { get; set; } = new();
}

public class PayloadRequest
{
    [Required]
    public string Type { get; set; } =  string.Empty;
    [Required]
    public string OrderId { get; set; }  = string.Empty;
}