using ConfigAdmin.Domain.Integration;
using ConfigAdmin.Domain.Models;

namespace ConfigAdmin.Integration.OneC;

public sealed class OneCCliAdapter : IOneCCliAdapter
{
    private readonly ProcessRunner _processRunner;
    private readonly TimeSpan _connectionTestTimeout;

    public OneCCliAdapter(ProcessRunner? processRunner = null, TimeSpan? connectionTestTimeout = null)
    {
        _processRunner = processRunner ?? new ProcessRunner();
        _connectionTestTimeout = connectionTestTimeout ?? TimeSpan.FromMinutes(5);
    }

    public string BuildConnectionArgs(ConnectionSettings settings) =>
        OneCCommandBuilder.BuildConnectionArgs(settings);

    public string BuildDumpConfigCommand(DumpConfigRequest request) =>
        OneCCommandBuilder.BuildDumpConfigCommand(request);

    public string MaskPassword(string commandLine) =>
        OneCCommandBuilder.MaskPassword(commandLine);

    public Task<ProcessResult> RunDesignerAsync(DesignerCommand command, CancellationToken ct = default) =>
        _processRunner.RunAsync(command.PlatformPath, command.Arguments, command.Timeout, ct);

    public Task<ProcessResult> TestConnectionAsync(
        InfobaseProfile profile,
        string? password,
        CancellationToken ct = default)
    {
        var arguments = OneCCommandBuilder.BuildTestConnectionCommand(profile, password);
        var command = new DesignerCommand
        {
            PlatformPath = profile.PlatformPath,
            Arguments = arguments,
            Timeout = _connectionTestTimeout
        };
        return RunDesignerAsync(command, ct);
    }
}
