namespace ConfigAdmin.Domain.Models;

public sealed class RemoteNodeProfile
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public byte[] PairingSecretVerifier { get; set; } = [];
    public string? HubListenUrl { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public string? AgentVersion { get; set; }
    public bool Enabled { get; set; } = true;
}
