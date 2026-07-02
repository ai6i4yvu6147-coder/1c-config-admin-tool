namespace ConfigAdmin.Domain.Models;

public sealed class DataConnection
{
    public Guid Id { get; set; }
    public Guid InfobaseId { get; set; }
    public string DatabaseId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}
