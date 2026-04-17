using System.Diagnostics;
using System.Security.Principal;

namespace Buff_App.Services;

public sealed class PrivilegeService : IPrivilegeService
{
    public bool IsRunningAsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public void RestartElevated(int? pendingInterfaceIndex = null)
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Buff could not resolve its executable path for elevation.");
        }

        var arguments = pendingInterfaceIndex.HasValue
            ? $"--set-primary {pendingInterfaceIndex.Value}"
            : string.Empty;

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            UseShellExecute = true,
            Verb = "runas"
        };

        Process.Start(startInfo);
    }
}
