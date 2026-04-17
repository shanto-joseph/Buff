using Buff_App.Models;

namespace Buff_App.Services;

public interface ISpeedTestService
{
    Task<SpeedTestResult> RunAsync(
        string? localIpAddress = null,
        CancellationToken cancellationToken = default,
    IProgress<SpeedTestProgress>? progress = null);
}
