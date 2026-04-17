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
}
