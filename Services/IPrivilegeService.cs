namespace Buff_App.Services;

public interface IPrivilegeService
{
    bool IsRunningAsAdministrator();

    void RestartElevated(int? pendingInterfaceIndex = null);
}
