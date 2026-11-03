namespace RelayForge.Models;

public class Event
{
    public Guid Id { get; set; }
    public string DestinationUrl { get; set; } =  string.Empty;
    public string PayloadJson { get; set; } =  string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    
    public string Status { get; set; } = "Pending";
    public int Attempts { get; set; } = 0;
    public DateTime? LastAttemptAtUtc { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
}