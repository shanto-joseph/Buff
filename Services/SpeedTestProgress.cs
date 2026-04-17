namespace Buff_App.Services;

public enum SpeedTestPhase
{
    Download,
    Upload
}

public sealed class SpeedTestProgress
{
    public required SpeedTestPhase Phase { get; init; }

    public required double Mbps { get; init; }

    // Overall progress from 0 to 100.
    public required double ProgressPercent { get; init; }
}
