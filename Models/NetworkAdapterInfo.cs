using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace Buff_App.Models;

public sealed class NetworkAdapterInfo
{
    public required string Name { get; init; }

    public required string AdapterType { get; init; }

    public required string StatusLabel { get; init; }

    public required string ConnectionState { get; init; }

    public required int InterfaceIndex { get; init; }

    public int? InterfaceMetric { get; init; }

    public int? RouteMetric { get; init; }

    public int? EffectiveMetric { get; init; }

    public bool HasDefaultRoute { get; init; }

    public string Ipv4Address { get; init; } = "Unavailable";

    public string Description { get; init; } = string.Empty;

    public bool IsPreferred { get; init; }

    public bool CanSetPrimary { get; init; }

    public bool IsConnected => string.Equals(StatusLabel, "Connected", StringComparison.Ordinal);

    public string MetricDisplay => InterfaceMetric?.ToString() ?? "Auto";

    public string RouteMetricDisplay => RouteMetric?.ToString() ?? "None";

    public string EffectiveMetricDisplay => EffectiveMetric?.ToString() ?? "Unavailable";

    public string DetailLine => $"{Description} | State: {ConnectionState}";

    public string ActionLabel => IsPreferred ? "Primary" : "Set Primary";

    public string RouteStatusLabel => HasDefaultRoute ? "Default route" : "No default route";

    public string PreferenceSummary =>
        IsPreferred
            ? $"Windows currently prefers this path with an effective metric of {EffectiveMetricDisplay}."
            : HasDefaultRoute
                ? $"Eligible for internet traffic with an effective metric of {EffectiveMetricDisplay}."
                : "Detected, but not currently carrying the default IPv4 route.";

    public Visibility PreferredBadgeVisibility => IsPreferred ? Visibility.Visible : Visibility.Collapsed;

    public Visibility DefaultRouteBadgeVisibility => HasDefaultRoute ? Visibility.Visible : Visibility.Collapsed;

    public SolidColorBrush AccentBorderBrush => new(IsPreferred ? Color.FromArgb(255, 83, 192, 40) : Color.FromArgb(255, 36, 39, 44));

    public SolidColorBrush StatusBadgeBrush =>
        StatusLabel switch
        {
            "Connected" => new SolidColorBrush(Color.FromArgb(255, 83, 192, 40)),
            "Disconnected" => new SolidColorBrush(Color.FromArgb(255, 138, 145, 153)),
            _ => new SolidColorBrush(Color.FromArgb(255, 255, 184, 77))
        };
}
