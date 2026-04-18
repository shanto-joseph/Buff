using System.Text.Json;
using Buff_App.Models;

namespace Buff_App.Services;

public sealed class PowerShellNetworkAdapterService : INetworkAdapterService
{
    public async Task<IReadOnlyList<NetworkAdapterInfo>> GetAdaptersAsync(CancellationToken cancellationToken = default)
    {
        const string script = """
            $adapters = Get-NetAdapter |
                Select-Object Name, InterfaceDescription, InterfaceIndex, @{N='Status';E={[string]$_.Status}}, @{N='MediaType';E={[string]$_.MediaType}}, @{N='PhysicalMediaType';E={[string]$_.PhysicalMediaType}};

            $interfaceByIndex = @{};
            Get-NetIPInterface -AddressFamily IPv4 |
                Select-Object InterfaceIndex, InterfaceMetric, @{N='ConnectionState';E={[string]$_.ConnectionState}} |
                ForEach-Object { $interfaceByIndex[$_.InterfaceIndex] = $_ };

            $addressByIndex = @{};
            Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
                Where-Object { $_.IPAddress -notlike '169.254*' } |
                Select-Object InterfaceIndex, IPAddress |
                ForEach-Object {
                    if (-not $addressByIndex.ContainsKey($_.InterfaceIndex)) {
                        $addressByIndex[$_.InterfaceIndex] = $_
                    }
                };

            $gatewayByIndex = @{};
            Get-NetRoute -AddressFamily IPv4 -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue |
                Where-Object { $_.NextHop -and $_.NextHop -ne '0.0.0.0' } |
                Sort-Object InterfaceIndex, RouteMetric |
                ForEach-Object {
                    if (-not $gatewayByIndex.ContainsKey($_.InterfaceIndex)) {
                        $gatewayByIndex[$_.InterfaceIndex] = $_.NextHop
                    }
                };

            $items = foreach ($adapter in $adapters) {
                $ipInterface = $interfaceByIndex[$adapter.InterfaceIndex];
                $ipAddress = $addressByIndex[$adapter.InterfaceIndex];
                $nextHop = $gatewayByIndex[$adapter.InterfaceIndex];

                [PSCustomObject]@{
                    Name = $adapter.Name;
                    Description = $adapter.InterfaceDescription;
                    InterfaceIndex = $adapter.InterfaceIndex;
                    Status = $adapter.Status;
                    MediaType = $adapter.MediaType;
                    PhysicalMediaType = $adapter.PhysicalMediaType;
                    InterfaceMetric = if ($ipInterface) { $ipInterface.InterfaceMetric } else { $null };
                    ConnectionState = if ($ipInterface) { [string]$ipInterface.ConnectionState } else { 'Unknown' };
                    IPv4Address = if ($ipAddress) { $ipAddress.IPAddress } else { $null };
                    RouteMetric = $null;
                    NextHop = $nextHop;
                    RouteState = if ($nextHop) { 'Active' } else { $null };
                    HasDefaultRoute = [bool]$nextHop
                }
            }

            $items | ConvertTo-Json -Depth 4 -Compress
            """;

        var json = await PowerShellCommandRunner.ExecuteAsync(script, cancellationToken);
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        var rawAdapters = JsonSerializer.Deserialize<List<RawNetworkAdapter>>(json, JsonOptions) ?? [];
        var preferredIndex = rawAdapters
            .Where(static adapter => adapter.HasDefaultRoute && IsConnectedStatus(adapter.Status))
            .OrderBy(static adapter => adapter.InterfaceMetric)
            .ThenBy(static adapter => adapter.EffectiveMetric)
            .ThenBy(static adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(static adapter => (int?)adapter.InterfaceIndex)
            .FirstOrDefault()
            ?? rawAdapters
                .Where(static adapter => adapter.InterfaceMetric.HasValue && IsConnectedStatus(adapter.Status))
                .OrderBy(static adapter => adapter.InterfaceMetric)
                .ThenBy(static adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
                .Select(static adapter => (int?)adapter.InterfaceIndex)
                .FirstOrDefault();

        return rawAdapters
            .OrderByDescending(static adapter => adapter.HasDefaultRoute)
            .ThenBy(static adapter => adapter.EffectiveMetric ?? int.MaxValue)
            .ThenBy(static adapter => adapter.Name, StringComparer.OrdinalIgnoreCase)
            .Select(adapter => adapter.ToModel(preferredIndex))
            .ToList();
    }

    private static bool IsConnectedStatus(string? status) =>
        string.Equals(status, "Up", StringComparison.OrdinalIgnoreCase);

    private static string ResolveAdapterType(RawNetworkAdapter adapter)
    {
        var combined = string.Join(' ', [adapter.MediaType, adapter.PhysicalMediaType, adapter.Name, adapter.Description]);

        if (combined.Contains("wi-fi", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("wifi", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("wireless", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("802.11", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("wlan", StringComparison.OrdinalIgnoreCase))
        {
            return "Wi-Fi";
        }

        if (combined.Contains("rndis", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("tether", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("remote ndis", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("mobile", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("iphone", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("android", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("usb", StringComparison.OrdinalIgnoreCase))
        {
            return "USB / Tethering";
        }

        if (combined.Contains("bluetooth", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("pan", StringComparison.OrdinalIgnoreCase))
        {
            return "Bluetooth Network";
        }

        if (combined.Contains("ethernet", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("802.3", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("gigabit", StringComparison.OrdinalIgnoreCase))
        {
            return "Ethernet";
        }

        if (combined.Contains("hyper-v", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("vmware", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("virtual", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("vethernet", StringComparison.OrdinalIgnoreCase) ||
            combined.Contains("wsl", StringComparison.OrdinalIgnoreCase))
        {
            return "Virtual Adapter";
        }

        return "Network Adapter";
    }

    private static string ResolveStatusLabel(RawNetworkAdapter adapter)
    {
        var isUp = string.Equals(adapter.Status, "Up", StringComparison.OrdinalIgnoreCase);
        var hasIpv4 = !string.IsNullOrWhiteSpace(adapter.IPv4Address);
        var isConnectedState = string.Equals(adapter.ConnectionState, "Connected", StringComparison.OrdinalIgnoreCase);

        if (isUp && hasIpv4 && (adapter.HasDefaultRoute || isConnectedState))
        {
            return "Connected";
        }

        if (isUp)
        {
            return "Limited";
        }

        if (string.Equals(adapter.Status, "Disconnected", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(adapter.Status, "Down", StringComparison.OrdinalIgnoreCase))
        {
            return "Disconnected";
        }

        return "Limited";
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private sealed class RawNetworkAdapter
    {
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public int InterfaceIndex { get; set; }

        public string Status { get; set; } = string.Empty;

        public string MediaType { get; set; } = string.Empty;

        public string PhysicalMediaType { get; set; } = string.Empty;

        public int? InterfaceMetric { get; set; }

        public int? RouteMetric { get; set; }

        public string ConnectionState { get; set; } = "Unknown";

        public string? IPv4Address { get; set; }

        public string? NextHop { get; set; }

        public string? RouteState { get; set; }

        public bool HasDefaultRoute { get; set; }

        public int? EffectiveMetric =>
            InterfaceMetric.HasValue && RouteMetric.HasValue
                ? InterfaceMetric.Value + RouteMetric.Value
                : InterfaceMetric;

        public NetworkAdapterInfo ToModel(int? preferredIndex) =>
            new()
            {
                Name = Name,
                AdapterType = ResolveAdapterType(this),
                StatusLabel = ResolveStatusLabel(this),
                ConnectionState = ConnectionState,
                InterfaceIndex = InterfaceIndex,
                InterfaceMetric = InterfaceMetric,
                RouteMetric = RouteMetric,
                EffectiveMetric = EffectiveMetric,
                HasDefaultRoute = HasDefaultRoute,
                Ipv4Address = string.IsNullOrWhiteSpace(IPv4Address) ? "Unavailable" : IPv4Address,
                Description = string.IsNullOrWhiteSpace(Description) ? "No adapter description available" : Description,
                IsPreferred = preferredIndex.HasValue && preferredIndex.Value == InterfaceIndex,
                CanSetPrimary = InterfaceMetric.HasValue
            };
    }
}
