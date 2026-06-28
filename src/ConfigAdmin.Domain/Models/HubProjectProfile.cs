namespace ConfigAdmin.Domain.Models;

public sealed class HubProjectProfile
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool Active { get; set; } = true;
}
