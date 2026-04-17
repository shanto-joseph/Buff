using Buff_App.Models;

namespace Buff_App.Services;

public sealed class NetworkPriorityService : INetworkPriorityService
{
    private const int PreferredMetric = 5;
    private const int SecondaryMetricStart = 25;
    private const int SecondaryMetricStep = 5;

    public async Task SetPrimaryAsync(NetworkAdapterInfo selectedAdapter, IReadOnlyList<NetworkAdapterInfo> adapters, CancellationToken cancellationToken = default)
    {
        var eligibleAdapters = adapters
            .Where(static adapter => adapter.InterfaceMetric.HasValue)
            .OrderBy(static adapter => adapter.InterfaceIndex)
            .ToList();

        if (eligibleAdapters.Count == 0)
        {
            throw new InvalidOperationException("No eligible IPv4 adapters were found for priority updates.");
        }

        var scripts = new List<string>
        {
            BuildMetricCommand(selectedAdapter.InterfaceIndex, PreferredMetric)
        };

        var secondaryMetric = SecondaryMetricStart;
        foreach (var adapter in eligibleAdapters.Where(adapter => adapter.InterfaceIndex != selectedAdapter.InterfaceIndex))
        {
            scripts.Add(BuildMetricCommand(adapter.InterfaceIndex, secondaryMetric));
            secondaryMetric += SecondaryMetricStep;
        }

        // Set metrics — give selected adapter the lowest interface metric
        var script = string.Join(Environment.NewLine, scripts);
        await PowerShellCommandRunner.ExecuteAsync(script, cancellationToken);

        // Set route metric to 0 on selected adapter so effective metric = interface metric
        // and flush route cache + DNS so Windows picks up the change immediately
        var idx = selectedAdapter.InterfaceIndex;
        var flushScript = $@"
$routes = Get-NetRoute -InterfaceIndex {idx} -DestinationPrefix '0.0.0.0/0' -ErrorAction SilentlyContinue
foreach ($route in $routes) {{
    $gw = $route.NextHop
    Remove-NetRoute -InterfaceIndex {idx} -DestinationPrefix '0.0.0.0/0' -NextHop $gw -Confirm:$false -ErrorAction SilentlyContinue
    New-NetRoute -InterfaceIndex {idx} -DestinationPrefix '0.0.0.0/0' -NextHop $gw -RouteMetric 0 -ErrorAction SilentlyContinue
}}
Clear-DnsClientCache -ErrorAction SilentlyContinue
";

        await PowerShellCommandRunner.ExecuteAsync(flushScript, cancellationToken);
    }

    private static string BuildMetricCommand(int interfaceIndex, int metric) =>
        $"Set-NetIPInterface -InterfaceIndex {interfaceIndex} -AddressFamily IPv4 -InterfaceMetric {metric} -ErrorAction Stop";
}
