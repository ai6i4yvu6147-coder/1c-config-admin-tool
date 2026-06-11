using ConfigAdmin.Domain.Enums;
using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Integration.OneC;

public static class OneCCommandBuilder
{
    public static string BuildConnectionArgs(ConnectionSettings settings) =>
        settings.BuildConnectionArgs();

    public static string BuildAuthArgs(string? username, string? password)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(username))
            parts.Add($"/N\"{username}\"");
        if (!string.IsNullOrWhiteSpace(password))
            parts.Add($"/P\"{password}\"");
        return string.Join(" ", parts);
    }

    public static string BuildDumpConfigCommand(DumpConfigRequest request)
    {
        request.Connection.Validate();

        var parts = new List<string>
        {
            "DESIGNER",
            request.Connection.BuildConnectionArgs(),
            BuildAuthArgs(request.Connection.Username, request.Connection.Password),
            "/DisableStartupDialogs",
            "/DisableStartupMessages",
            $"/DumpConfigToFiles \"{request.OutputPath}\""
        };

        if (request.AllExtensions)
            parts.Add("-AllExtensions");

        if (!string.IsNullOrWhiteSpace(request.ExtensionName))
            parts.Add($"-Extension \"{request.ExtensionName}\"");

        if (request.Format == ExportFormat.Plain)
            parts.Add("-Format Plain");
        else
            parts.Add("-Format Hierarchical");

        if (!string.IsNullOrWhiteSpace(request.OutLogPath))
            parts.Add($"/Out \"{request.OutLogPath}\"");

        if (!string.IsNullOrWhiteSpace(request.DumpResultPath))
            parts.Add($"/DumpResult \"{request.DumpResultPath}\"");

        return string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    public static string BuildOpenConfiguratorCommand(ConnectionSettings settings)
    {
        settings.Validate();

        return string.Join(" ",
            "DESIGNER",
            settings.BuildConnectionArgs(),
            BuildAuthArgs(settings.Username, settings.Password));
    }

    public static string BuildTestConnectionCommand(InfobaseProfile profile, string? password)
    {
        var connection = new ConnectionSettings
        {
            ConnectionType = profile.ConnectionType,
            ConnectionString = profile.ConnectionString,
            Username = profile.Username,
            Password = password
        };

        return string.Join(" ",
            "DESIGNER",
            connection.BuildConnectionArgs(),
            BuildAuthArgs(connection.Username, connection.Password),
            "/DisableStartupDialogs",
            "/DisableStartupMessages");
    }

    public static string MaskPassword(string commandLine) =>
        System.Text.RegularExpressions.Regex.Replace(
            commandLine,
            @"/P""[^""]*""",
            "/P\"***\"",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
}
