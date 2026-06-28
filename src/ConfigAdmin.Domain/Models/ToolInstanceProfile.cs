namespace ConfigAdmin.Domain.Models;

public sealed class ToolInstanceProfile
{
    public Guid Id { get; set; }
    public string ModuleId { get; set; } = string.Empty;
    public string RootPath { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}
