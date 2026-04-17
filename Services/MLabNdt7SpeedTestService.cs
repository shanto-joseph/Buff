using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Buff_App.Models;

namespace Buff_App.Services;

public sealed class MLabNdt7SpeedTestService : ISpeedTestService
{
    private static readonly Uri LocateServiceUri = new("https://locate.measurementlab.net/v2/nearest/ndt/ndt7");
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly TimeSpan DownloadTargetDuration = TimeSpan.FromSeconds(6);
    private static readonly TimeSpan UploadTargetDuration = TimeSpan.FromSeconds(6);

    public async Task<SpeedTestResult> RunAsync(
        string? localIpAddress = null,
        CancellationToken cancellationToken = default,
        IProgress<SpeedTestProgress>? progress = null)
    {
        var localEndPoint = ResolveLocalEndPoint(localIpAddress);
        using var httpClient = BuildHttpClient(localEndPoint);

        var target = await DiscoverTargetAsync(httpClient, cancellationToken);
        var downloadSummary = await RunDownloadTestAsync(target.DownloadUrl, localEndPoint, cancellationToken, progress);
        var uploadSummary = await RunUploadTestAsync(target.UploadUrl, localEndPoint, cancellationToken, progress);

        var rttSamples = downloadSummary.RttSamples.Concat(uploadSummary.RttSamples).ToList();
        var minRttMilliseconds = rttSamples.Count > 0 ? rttSamples.Min() : 0d;
        var jitterMilliseconds = CalculateJitter(rttSamples);

        return new SpeedTestResult
        {
            DownloadMbps = downloadSummary.ThroughputMbps,
            UploadMbps = uploadSummary.ThroughputMbps,
            PingMilliseconds = Math.Round(minRttMilliseconds, 2),
            JitterMilliseconds = Math.Round(jitterMilliseconds, 2),
            ServerName = target.ServerDisplayName,
            IspName = "M-Lab NDT7",
            Timestamp = DateTimeOffset.Now,
            ResultUrl = target.MachineName
        };
    }

    private static IPEndPoint? ResolveLocalEndPoint(string? localIpAddress)
    {
        if (string.IsNullOrWhiteSpace(localIpAddress) ||
            localIpAddress == "Unavailable" ||
            !IPAddress.TryParse(localIpAddress, out var ip))
        {
            return null;
        }

        return new IPEndPoint(ip, 0);
    }

    private static HttpClient BuildHttpClient(IPEndPoint? localEndPoint)
    {
        var handler = new SocketsHttpHandler
        {
            ConnectCallback = localEndPoint is null
                ? null
                : async (context, ct) =>
                {
                    var socket = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    socket.Bind(localEndPoint);
                    await socket.ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, ct);
                    return new NetworkStream(socket, ownsSocket: true);
                }
        };

        var client = new HttpClient(handler);
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Buff/0.1");
        return client;
    }

    private async Task<LocateTarget> DiscoverTargetAsync(HttpClient httpClient, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(LocateServiceUri, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var payload = JsonSerializer.Deserialize<LocateResponse>(json, JsonOptions)
            ?? throw new InvalidOperationException("Buff could not understand the M-Lab locate response.");

        var first = payload.Results?.FirstOrDefault(result =>
            result.Urls is not null &&
            result.Urls.TryGetValue("wss:///ndt/v7/download", out _) &&
            result.Urls.TryGetValue("wss:///ndt/v7/upload", out _))
            ?? throw new InvalidOperationException("Buff could not find an available M-Lab NDT7 server.");

        var downloadUrl = AppendMetadata(first.Urls!["wss:///ndt/v7/download"]);
        var uploadUrl = AppendMetadata(first.Urls["wss:///ndt/v7/upload"]);
        var city = first.Location?.City;
        var country = first.Location?.Country;
        var displayName = string.IsNullOrWhiteSpace(city)
            ? first.Machine ?? "M-Lab server"
            : string.IsNullOrWhiteSpace(country)
                ? city
                : $"{city}, {country}";

        return new LocateTarget
        {
            DownloadUrl = new Uri(downloadUrl),
            UploadUrl = new Uri(uploadUrl),
            ServerDisplayName = displayName,
            MachineName = first.Machine ?? "M-Lab"
        };
    }

    private async Task<TestRunSummary> RunDownloadTestAsync(
        Uri downloadUrl,
        IPEndPoint? localEndPoint,
        CancellationToken cancellationToken,
        IProgress<SpeedTestProgress>? progress)
    {
        using var socket = await ConnectAsync(downloadUrl, localEndPoint, cancellationToken);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedCts.CancelAfter(DownloadTargetDuration);
        var stopwatch = Stopwatch.StartNew();
        var summary = new TestRunSummary();

        await ReceiveLoopAsync(
            socket,
            onBinaryMessage: count =>
            {
                summary.LocalBytes += count;
                if (stopwatch.Elapsed > TimeSpan.Zero)
                {
                    var currentMbps = (summary.LocalBytes * 8d) / stopwatch.Elapsed.TotalSeconds / 1_000_000d;
                    var phaseCompletion = Math.Min(stopwatch.Elapsed.TotalMilliseconds / DownloadTargetDuration.TotalMilliseconds, 1d);
                    progress?.Report(new SpeedTestProgress
                    {
                        Phase = SpeedTestPhase.Download,
                        Mbps = Math.Round(currentMbps, 2),
                        ProgressPercent = Math.Round(phaseCompletion * 50d, 1)
                    });
                }
            },
            onTextMessage: text =>
            {
                summary.RegisterMeasurement(text);
            },
            linkedCts.Token);

        stopwatch.Stop();

        summary.FinalizeThroughput(stopwatch.Elapsed);
        return summary;
    }

    private async Task<TestRunSummary> RunUploadTestAsync(
        Uri uploadUrl,
        IPEndPoint? localEndPoint,
        CancellationToken cancellationToken,
        IProgress<SpeedTestProgress>? progress)
    {
        using var socket = await ConnectAsync(uploadUrl, localEndPoint, cancellationToken);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var summary = new TestRunSummary();
        var stopwatch = Stopwatch.StartNew();
        var payload = GC.AllocateUninitializedArray<byte>(1 << 13);
        Random.Shared.NextBytes(payload);

        var receiveTask = ReceiveLoopAsync(
            socket,
            onBinaryMessage: _ => { },
            onTextMessage: text => summary.RegisterMeasurement(text),
            linkedCts.Token);

        try
        {
                 while (stopwatch.Elapsed < UploadTargetDuration &&
                   socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
            {
                if (socket.State == WebSocketState.CloseReceived)
                {
                    break;
                }

                await socket.SendAsync(payload, WebSocketMessageType.Binary, true, cancellationToken);
                summary.LocalBytes += payload.Length;
                if (stopwatch.Elapsed > TimeSpan.Zero)
                {
                    var currentMbps = (summary.LocalBytes * 8d) / stopwatch.Elapsed.TotalSeconds / 1_000_000d;
                    var phaseCompletion = Math.Min(stopwatch.Elapsed.TotalMilliseconds / UploadTargetDuration.TotalMilliseconds, 1d);
                    progress?.Report(new SpeedTestProgress
                    {
                        Phase = SpeedTestPhase.Upload,
                        Mbps = Math.Round(currentMbps, 2),
                        ProgressPercent = Math.Round(50d + (phaseCompletion * 50d), 1)
                    });
                }
            }
        }
        catch (WebSocketException)
        {
            // Treat abrupt close as end-of-test per ndt7 robustness guidance.
        }
        finally
        {
            stopwatch.Stop();

            if (socket.State == WebSocketState.Open)
            {
                try
                {
                    await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "upload-complete", CancellationToken.None);
                }
                catch (WebSocketException)
                {
                }
            }

            linkedCts.CancelAfter(TimeSpan.FromSeconds(2));
            try
            {
                await receiveTask;
            }
            catch (OperationCanceledException) when (linkedCts.IsCancellationRequested)
            {
            }
        }

        summary.FinalizeThroughput(stopwatch.Elapsed);
        progress?.Report(new SpeedTestProgress
        {
            Phase = SpeedTestPhase.Upload,
            Mbps = summary.ThroughputMbps,
            ProgressPercent = 100d
        });
        return summary;
    }

    private static async Task<ClientWebSocket> ConnectAsync(Uri uri, IPEndPoint? localEndPoint, CancellationToken cancellationToken)
    {
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol("net.measurementlab.ndt.v7");

        if (localEndPoint is not null)
        {
            // When using HttpMessageInvoker, headers must go on the handler, not socket.Options
            var handler = new SocketsHttpHandler
            {
                ConnectCallback = async (context, ct) =>
                {
                    var s = new Socket(localEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    s.Bind(localEndPoint);
                    await s.ConnectAsync(context.DnsEndPoint.Host, context.DnsEndPoint.Port, ct);
                    return new NetworkStream(s, ownsSocket: true);
                }
            };
            handler.RequestHeaderEncodingSelector = (_, _) => System.Text.Encoding.UTF8;

            var invoker = new HttpMessageInvoker(handler);
            await socket.ConnectAsync(uri, invoker, cancellationToken);
        }
        else
        {
            socket.Options.SetRequestHeader("User-Agent", "Buff/0.1");
            await socket.ConnectAsync(uri, cancellationToken);
        }

        return socket;
    }

    private static async Task ReceiveLoopAsync(
        ClientWebSocket socket,
        Action<int> onBinaryMessage,
        Action<string> onTextMessage,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[1 << 15];
        using var textBuffer = new MemoryStream();

        while (!cancellationToken.IsCancellationRequested &&
               socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            WebSocketReceiveResult result;
            try
            {
                result = await socket.ReceiveAsync(buffer, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (WebSocketException)
            {
                break;
            }

            if (result.MessageType == WebSocketMessageType.Close)
            {
                if (socket.State == WebSocketState.CloseReceived)
                {
                    try
                    {
                        await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "ack-close", CancellationToken.None);
                    }
                    catch (WebSocketException)
                    {
                    }
                }

                break;
            }

            if (result.MessageType == WebSocketMessageType.Binary)
            {
                onBinaryMessage(result.Count);
                continue;
            }

            textBuffer.Write(buffer, 0, result.Count);
            if (!result.EndOfMessage)
            {
                continue;
            }

            var text = Encoding.UTF8.GetString(textBuffer.GetBuffer(), 0, checked((int)textBuffer.Length));
            textBuffer.SetLength(0);
            onTextMessage(text);
        }
    }

    private static string AppendMetadata(string url)
    {
        var separator = url.Contains('?', StringComparison.Ordinal) ? "&" : "?";
        return $"{url}{separator}client_name=buff&client_version=0.1.0";
    }

    private static double CalculateJitter(IReadOnlyList<double> rttSamples)
    {
        if (rttSamples.Count < 2)
        {
            return 0d;
        }

        var deltas = new List<double>(rttSamples.Count - 1);
        for (var index = 1; index < rttSamples.Count; index++)
        {
            deltas.Add(Math.Abs(rttSamples[index] - rttSamples[index - 1]));
        }

        return deltas.Average();
    }

    private sealed class LocateTarget
    {
        public required Uri DownloadUrl { get; init; }

        public required Uri UploadUrl { get; init; }

        public required string ServerDisplayName { get; init; }

        public required string MachineName { get; init; }
    }

    private sealed class TestRunSummary
    {
        private NdtMeasurement? _lastMeasurement;

        public long LocalBytes { get; set; }

        public List<double> RttSamples { get; } = [];

        public double ThroughputMbps { get; private set; }

        public void RegisterMeasurement(string json)
        {
            NdtMeasurement? measurement;
            try
            {
                measurement = JsonSerializer.Deserialize<NdtMeasurement>(json, JsonOptions);
            }
            catch (JsonException)
            {
                return;
            }

            if (measurement is null)
            {
                return;
            }

            _lastMeasurement = measurement;

            if (measurement.TcpInfo?.MinRtt is long minRttMicros)
            {
                RttSamples.Add(minRttMicros / 1000d);
            }
            else if (measurement.TcpInfo?.Rtt is long rttMicros)
            {
                RttSamples.Add(rttMicros / 1000d);
            }
        }

        public void FinalizeThroughput(TimeSpan elapsed)
        {
            ThroughputMbps = TryGetMeasurementThroughputMbps() ?? ComputeThroughputMbps(LocalBytes, elapsed);
        }

        public double? TryGetCurrentThroughputMbps() => TryGetMeasurementThroughputMbps();

        private double? TryGetMeasurementThroughputMbps()
        {
            if (_lastMeasurement?.AppInfo is not { ElapsedTime: > 0, NumBytes: > 0 } appInfo)
            {
                return null;
            }

            return Math.Round((appInfo.NumBytes * 8d) / appInfo.ElapsedTime, 2);
        }

        private static double ComputeThroughputMbps(long bytes, TimeSpan elapsed)
        {
            if (bytes <= 0 || elapsed <= TimeSpan.Zero)
            {
                return 0d;
            }

            return Math.Round((bytes * 8d) / elapsed.TotalSeconds / 1_000_000d, 2);
        }
    }

    private sealed class LocateResponse
    {
        public List<LocateResult>? Results { get; set; }
    }

    private sealed class LocateResult
    {
        public string? Machine { get; set; }

        public LocateLocation? Location { get; set; }

        public Dictionary<string, string>? Urls { get; set; }
    }

    private sealed class LocateLocation
    {
        public string? City { get; set; }

        public string? Country { get; set; }
    }

    private sealed class NdtMeasurement
    {
        public NdtAppInfo? AppInfo { get; set; }

        public NdtTcpInfo? TcpInfo { get; set; }
    }

    private sealed class NdtAppInfo
    {
        public long ElapsedTime { get; set; }

        public long NumBytes { get; set; }
    }

    private sealed class NdtTcpInfo
    {
        public long? MinRtt { get; set; }

        public long? Rtt { get; set; }
    }
}
