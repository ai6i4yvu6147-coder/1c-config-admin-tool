using ConfigAdmin.Domain.Enums;

namespace ConfigAdmin.Domain.Models;

public sealed class ConnectionSettings
{
    public ConnectionType ConnectionType { get; init; }
    public string ConnectionString { get; init; } = string.Empty;
    public string? Username { get; init; }
    public string? Password { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
            throw new ArgumentException("Строка подключения не задана.", nameof(ConnectionString));

        if (ConnectionType is not (ConnectionType.File or ConnectionType.Server))
            throw new ArgumentException("Неподдерживаемый тип подключения.", nameof(ConnectionType));
    }

    public string BuildConnectionArgs()
    {
        Validate();
        return ConnectionType switch
        {
            ConnectionType.File => $"/F \"{ConnectionString}\"",
            ConnectionType.Server => $"/S \"{ConnectionString}\"",
            _ => throw new InvalidOperationException("Неподдерживаемый тип подключения.")
        };
    }
}
