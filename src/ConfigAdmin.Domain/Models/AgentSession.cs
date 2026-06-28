namespace ConfigAdmin.Domain.Models;

public sealed class AgentSession
{
    public byte[] TokenHash { get; set; } = [];
    public Guid NodeId { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
}
