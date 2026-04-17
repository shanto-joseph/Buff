namespace Buff_App.Models;

public sealed class SpeedTestResult
{
    public required double DownloadMbps { get; init; }

    public required double UploadMbps { get; init; }

    public required double PingMilliseconds { get; init; }

    public required double JitterMilliseconds { get; init; }

    public string ServerName { get; init; } = "Unknown server";

    public string IspName { get; init; } = "Unknown ISP";

    public DateTimeOffset Timestamp { get; init; }

    public string ResultUrl { get; init; } = string.Empty;

    public string DownloadDisplay => $"{DownloadMbps:F2} Mbps";

    public string UploadDisplay => $"{UploadMbps:F2} Mbps";

    public string PingDisplay => $"{PingMilliseconds:F2} ms";

    public string JitterDisplay => $"{JitterMilliseconds:F2} ms";

    public string TimestampDisplay => Timestamp.LocalDateTime.ToString("g");
}
