using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Text.Json;
using Buff_App.Models;

namespace Buff_App.Services;

/// <summary>
/// Runs a speed test using the Ookla Speedtest CLI (speedtest.exe).
/// Downloads the CLI on first use if not already present.
/// Supports binding to a specific network interface by IP address.
/// </summary>
public sealed class OoklaSpeedTestService : ISpeedTestService
{
    private static readonly string CliDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Buff", "speedtest");

    private static readonly string CliPath = Path.Combine(CliDir, "speedtest.exe");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<SpeedTestResult> RunAsync(
        string? localIpAddress = null,
        CancellationToken cancellationToken = default,
        IProgress<SpeedTestProgress>? progress = null)
    {
        await EnsureCliAsync(cancellationToken);

        var args = "--format=json --accept-license --accept-gdpr";

        // Bind to specific interface if IP provided
        if (!string.IsNullOrWhiteSpace(localIpAddress) && localIpAddress != "Unavailable")
        {
            args += $" --ip {localIpAddress}";
        }

        var output = await RunCliAsync(args, cancellationToken);
        var result = ParseResult(output);
        progress?.Report(new SpeedTestProgress { Phase = SpeedTestPhase.Download, Mbps = result.DownloadMbps });
        progress?.Report(new SpeedTestProgress { Phase = SpeedTestPhase.Upload, Mbps = result.UploadMbps });
        return result;
    }

    private static async Task EnsureCliAsync(CancellationToken cancellationToken)
    {
        if (File.Exists(CliPath)) return;

        Directory.CreateDirectory(CliDir);

        // Download Ookla speedtest CLI for Windows x64
        const string downloadUrl = "https://install.speedtest.net/app/cli/ookla-speedtest-1.2.0-win64.zip";
        var zipPath = Path.Combine(CliDir, "speedtest.zip");

        using var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Buff/1.0");

        var bytes = await http.GetByteArrayAsync(downloadUrl, cancellationToken);
        await File.WriteAllBytesAsync(zipPath, bytes, cancellationToken);

        System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, CliDir, overwriteFiles: true);
        File.Delete(zipPath);

        if (!File.Exists(CliPath))
            throw new InvalidOperationException("Speedtest CLI could not be extracted.");
    }

    private static async Task<string> RunCliAsync(string args, CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = CliPath,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = psi };
        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);

        var output = await outputTask;
        var error = await errorTask;

        if (process.ExitCode != 0 && string.IsNullOrWhiteSpace(output))
            throw new InvalidOperationException($"Speedtest CLI failed: {error}");

        return output;
    }

    private static SpeedTestResult ParseResult(string json)
    {
        var doc = JsonSerializer.Deserialize<OoklaResult>(json, JsonOptions)
            ?? throw new InvalidOperationException("Could not parse speedtest result.");

        return new SpeedTestResult
        {
            DownloadMbps = Math.Round((doc.Download?.Bandwidth ?? 0) * 8d / 1_000_000d, 2),
            UploadMbps = Math.Round((doc.Upload?.Bandwidth ?? 0) * 8d / 1_000_000d, 2),
            PingMilliseconds = Math.Round(doc.Ping?.Latency ?? 0, 2),
            JitterMilliseconds = Math.Round(doc.Ping?.Jitter ?? 0, 2),
            ServerName = doc.Server?.Name ?? "Unknown",
            IspName = doc.Isp ?? "Unknown ISP",
            Timestamp = DateTimeOffset.Now,
            ResultUrl = doc.Result?.Url ?? string.Empty
        };
    }

    // ── JSON model ──────────────────────────────────────────────────────────

    private sealed class OoklaResult
    {
        public OoklaPing? Ping { get; set; }
        public OoklaTransfer? Download { get; set; }
        public OoklaTransfer? Upload { get; set; }
        public OoklaServer? Server { get; set; }
        public string? Isp { get; set; }
        public OoklaResultInfo? Result { get; set; }
    }

    private sealed class OoklaPing
    {
        public double Jitter { get; set; }
        public double Latency { get; set; }
    }

    private sealed class OoklaTransfer
    {
        public long Bandwidth { get; set; }   // bytes/sec
        public long Bytes { get; set; }
        public long Elapsed { get; set; }
    }

    private sealed class OoklaServer
    {
        public string? Name { get; set; }
        public string? Location { get; set; }
        public string? Country { get; set; }
        public string? Host { get; set; }
        public int Port { get; set; }
        public int Id { get; set; }
    }

    private sealed class OoklaResultInfo
    {
        public string? Url { get; set; }
    }
}
