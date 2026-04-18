using System.Collections.ObjectModel;
using System.ComponentModel;
using Buff_App.Models;
using Buff_App.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Threading;
using Windows.Networking.Connectivity;

namespace Buff_App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly INetworkAdapterService _adapterService;
    private readonly INetworkPriorityService _priorityService;
    private readonly IPrivilegeService _privilegeService;
    private readonly ISpeedTestService _speedTestService;
    private readonly DispatcherQueue _dispatcherQueue;
    private readonly object _refreshGate = new();
    private CancellationTokenSource? _pollingCts;
    private CancellationTokenSource? _refreshCancellationSource;
    private Task? _refreshTask;
    private int _refreshRequested;
    private bool _networkWatcherStarted;

    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RefreshTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SpeedTestTimeout = TimeSpan.FromSeconds(45);

    public MainViewModel(
        INetworkAdapterService adapterService,
        INetworkPriorityService priorityService,
        IPrivilegeService privilegeService,
        ISpeedTestService speedTestService)
    {
        _adapterService = adapterService;
        _priorityService = priorityService;
        _privilegeService = privilegeService;
        _speedTestService = speedTestService;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => !IsBusy);
        SetPrimaryCommand = new AsyncRelayCommand<AdapterViewModel>(SetPrimaryAsync, CanSetPrimary);
        ElevateCommand = new RelayCommand(Elevate, CanElevate);
        RunSpeedTestCommand = new AsyncRelayCommand(RunSpeedTestAsync, CanRunSpeedTest);
    }

    public ObservableCollection<AdapterViewModel> Adapters { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(AdapterCount))]
    [NotifyPropertyChangedFor(nameof(ConnectedAdapterCount))]
    [NotifyPropertyChangedFor(nameof(SummaryTitle))]
    [NotifyPropertyChangedFor(nameof(SummarySubtitle))]
    [NotifyPropertyChangedFor(nameof(EmptyStateVisibility))]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBannerMessage))]
    public partial string BannerTitle { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBannerMessage))]
    public partial string BannerMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity BannerSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsAdministrator { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedTestStatusTitle))]
    [NotifyPropertyChangedFor(nameof(SpeedTestStatusMessage))]
    [NotifyPropertyChangedFor(nameof(SpeedTestResultVisibility))]
    [NotifyPropertyChangedFor(nameof(SpeedDisplayValue))]
    [NotifyPropertyChangedFor(nameof(IsTestViaSelectable))]
    public partial bool IsSpeedTestRunning { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedTestResultVisibility))]
    [NotifyPropertyChangedFor(nameof(SpeedTestStatusTitle))]
    [NotifyPropertyChangedFor(nameof(SpeedTestStatusMessage))]
    [NotifyPropertyChangedFor(nameof(SpeedDisplayValue))]
    [NotifyPropertyChangedFor(nameof(DownloadCardDisplay))]
    [NotifyPropertyChangedFor(nameof(UploadCardDisplay))]
    [NotifyPropertyChangedFor(nameof(AdditionalResultVisibility))]
    public partial SpeedTestResult? LatestSpeedTestResult { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedTestStatusMessage))]
    public partial string SpeedTestAdapterName { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedDisplayValue))]
    public partial double LiveDownloadMbps { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedTestProgressVisibility))]
    public partial double SpeedTestProgressPercent { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UploadCardDisplay))]
    public partial double LiveUploadMbps { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DownloadCardDisplay))]
    public partial double CompletedDownloadMbps { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedDisplayPhaseLabel))]
    [NotifyPropertyChangedFor(nameof(SpeedDisplayValue))]
    public partial SpeedTestPhase CurrentSpeedPhase { get; set; } = SpeedTestPhase.Download;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedResultGridVisibility))]
    [NotifyPropertyChangedFor(nameof(DownloadResultVisibility))]
    public partial bool ShowDownloadResult { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedResultGridVisibility))]
    [NotifyPropertyChangedFor(nameof(UploadResultVisibility))]
    public partial bool ShowUploadResult { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SpeedResultGridVisibility))]
    [NotifyPropertyChangedFor(nameof(AdditionalResultVisibility))]
    public partial bool ShowAdditionalResults { get; set; }

    public IAsyncRelayCommand RefreshCommand { get; }
    public IAsyncRelayCommand<AdapterViewModel> SetPrimaryCommand { get; }
    public IRelayCommand ElevateCommand { get; }
    public IAsyncRelayCommand RunSpeedTestCommand { get; }

    [ObservableProperty]
    public partial AdapterViewModel? SelectedTestAdapter { get; set; }

    public int AdapterCount => Adapters.Count;

    public int ConnectedAdapterCount => Adapters.Count(a => a.Info.IsConnected);

    public string SummaryTitle =>
        Adapters.FirstOrDefault(a => a.Info.IsPreferred) is { } preferred
            ? $"Primary route: {preferred.Info.Name}"
            : "Pick a preferred connection";

    public string SummarySubtitle =>
        Adapters.Count == 0
            ? "No usable adapters discovered yet."
            : Adapters.FirstOrDefault(a => a.Info.IsPreferred) is { } preferred
                ? preferred.Info.PreferenceSummary
                : "Buff updates IPv4 interface metrics so Windows prefers the adapter you select.";

    public bool HasBannerMessage => !string.IsNullOrWhiteSpace(BannerMessage);

    public bool IsTestViaSelectable => !IsSpeedTestRunning;

    public Visibility EmptyStateVisibility => Adapters.Count == 0 && !IsBusy ? Visibility.Visible : Visibility.Collapsed;

    public string SpeedTestStatusTitle =>
        IsSpeedTestRunning
            ? string.Empty
            : LatestSpeedTestResult is not null
                ? "Latest M-Lab NDT7 Result"
                : "Ready to Test";

    public string SpeedDisplayValue =>
        IsSpeedTestRunning
            ? LiveDownloadMbps.ToString("F2")
            : LatestSpeedTestResult is null
                ? "0.00"
                : LatestSpeedTestResult.DownloadMbps.ToString("F2");

    public string SpeedDisplayPhaseLabel =>
        IsSpeedTestRunning
            ? (CurrentSpeedPhase == SpeedTestPhase.Upload ? "UPLOAD" : "DOWNLOAD")
            : "DOWNLOAD";

    public string DownloadCardDisplay =>
        LatestSpeedTestResult is not null
            ? LatestSpeedTestResult.DownloadDisplay
            : $"{CompletedDownloadMbps:F2} Mbps";

    public string UploadCardDisplay =>
        LatestSpeedTestResult is not null
            ? LatestSpeedTestResult.UploadDisplay
            : $"{LiveUploadMbps:F2} Mbps";

    public string SpeedTestStatusMessage
    {
        get
        {
            if (IsSpeedTestRunning)
                return "Testing your current route with M-Lab NDT7 over secure WebSockets.";
            if (LatestSpeedTestResult is not null)
                return string.IsNullOrWhiteSpace(SpeedTestAdapterName)
                    ? $"{LatestSpeedTestResult.IspName} · {LatestSpeedTestResult.ServerName} · {LatestSpeedTestResult.TimestampDisplay}"
                    : $"{SpeedTestAdapterName} · {LatestSpeedTestResult.ServerName} · {LatestSpeedTestResult.TimestampDisplay}";
            return "Runs a native speed test with M-Lab NDT7. No external executable required.";
        }
    }

    public Visibility SpeedTestResultVisibility => LatestSpeedTestResult is null ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SpeedTestProgressVisibility => IsSpeedTestRunning ? Visibility.Visible : Visibility.Collapsed;

    public Visibility SpeedResultGridVisibility =>
        ShowDownloadResult || ShowUploadResult || ShowAdditionalResults
            ? Visibility.Visible
            : Visibility.Collapsed;

    public Visibility DownloadResultVisibility =>
        ShowDownloadResult ? Visibility.Visible : Visibility.Collapsed;

    public Visibility UploadResultVisibility =>
        ShowUploadResult ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AdditionalResultVisibility =>
        LatestSpeedTestResult is not null && ShowAdditionalResults ? Visibility.Visible : Visibility.Collapsed;

    public async Task InitializeAsync(int? pendingInterfaceIndex = null)
    {
        IsAdministrator = _privilegeService.IsRunningAsAdministrator();
        ElevateCommand.NotifyCanExecuteChanged();
        RunSpeedTestCommand.NotifyCanExecuteChanged();

        await RefreshAsync();

        StartNetworkWatcher();
        StartPolling();

        if (pendingInterfaceIndex.HasValue)
        {
            var pending = Adapters.FirstOrDefault(a => a.Info.InterfaceIndex == pendingInterfaceIndex.Value);
            if (pending is null)
                ShowBanner("Adapter not found", "The requested adapter was not found after relaunch.", InfoBarSeverity.Warning);
            else
                await SetPrimaryAsync(pending);
        }
    }

    public void StopPolling()
    {
        StopNetworkWatcher();
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = null;

        lock (_refreshGate)
        {
            _refreshCancellationSource?.Cancel();
        }
    }

    private void StartNetworkWatcher()
    {
        if (_networkWatcherStarted)
        {
            return;
        }

        NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
        NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
        _networkWatcherStarted = true;
    }

    private void StopNetworkWatcher()
    {
        if (!_networkWatcherStarted)
        {
            return;
        }

        NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
        NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
        NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
        _networkWatcherStarted = false;
    }

    private void OnNetworkStatusChanged(object? sender)
    {
        QueueRefreshFromNetworkChange(cancelInFlight: false);
    }

    private void OnNetworkAddressChanged(object? sender, EventArgs e)
    {
        QueueRefreshFromNetworkChange(cancelInFlight: false);
    }

    private void OnNetworkAvailabilityChanged(object? sender, EventArgs e)
    {
        QueueRefreshFromNetworkChange(cancelInFlight: false);
    }

    private void StartPolling()
    {
        _pollingCts?.Cancel();
        _pollingCts?.Dispose();
        _pollingCts = new CancellationTokenSource();
        _ = PollLoopAsync(_pollingCts.Token);
    }

    private async Task PollLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(PollingInterval, cancellationToken);
                if (!cancellationToken.IsCancellationRequested)
                {
                    QueueRefreshFromNetworkChange(cancelInFlight: false);
                }
            }
            catch (OperationCanceledException) { break; }
            catch { /* swallow background errors */ }
        }
    }

    private void QueueRefreshFromNetworkChange(bool cancelInFlight)
    {
        _ = _dispatcherQueue.TryEnqueue(() =>
        {
            _ = QueueSilentRefreshAsync(cancelInFlight);
        });
    }

    private Task QueueSilentRefreshAsync(bool cancelInFlight, CancellationToken cancellationToken = default)
    {
        Interlocked.Exchange(ref _refreshRequested, 1);

        lock (_refreshGate)
        {
            if (_refreshTask is { IsCompleted: false })
            {
                if (cancelInFlight)
                {
                    _refreshCancellationSource?.Cancel();
                }

                return _refreshTask;
            }

            _refreshTask = RunSilentRefreshQueueAsync(cancellationToken);
            return _refreshTask;
        }
    }

    private async Task RunSilentRefreshQueueAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (Interlocked.Exchange(ref _refreshRequested, 0) == 1)
            {
                using var refreshCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                lock (_refreshGate)
                {
                    _refreshCancellationSource = refreshCts;
                }

                try
                {
                    refreshCts.CancelAfter(RefreshTimeout);
                    await SilentRefreshAsync(refreshCts.Token);
                }
                catch (OperationCanceledException)
                {
                    // A newer network event superseded this refresh or it timed out.
                }
                catch
                {
                    // Ignore transient network-state refresh errors; polling loop remains as fallback.
                }
                finally
                {
                    lock (_refreshGate)
                    {
                        if (ReferenceEquals(_refreshCancellationSource, refreshCts))
                        {
                            _refreshCancellationSource = null;
                        }
                    }
                }
            }
        }
        finally
        {
            lock (_refreshGate)
            {
                _refreshTask = null;
            }

            if (Interlocked.Exchange(ref _refreshRequested, 0) == 1)
            {
                RequestRefreshAgain();
            }
        }
    }

    private void RequestRefreshAgain()
    {
        _ = _dispatcherQueue.TryEnqueue(() =>
        {
            _ = QueueSilentRefreshAsync(cancelInFlight: false);
        });
    }

    private async Task SilentRefreshAsync(CancellationToken cancellationToken)
    {
        var adapters = await _adapterService.GetAdaptersAsync(cancellationToken);

        // Remove stale
        for (var i = Adapters.Count - 1; i >= 0; i--)
        {
            if (!adapters.Any(a => a.InterfaceIndex == Adapters[i].Info.InterfaceIndex))
                Adapters.RemoveAt(i);
        }

        // Update / insert
        for (var i = 0; i < adapters.Count; i++)
        {
            var fresh = adapters[i];
            var existing = Adapters.FirstOrDefault(a => a.Info.InterfaceIndex == fresh.InterfaceIndex);
            if (existing is null)
            {
                Adapters.Insert(i, new AdapterViewModel(fresh));
            }
            else
            {
                var idx = Adapters.IndexOf(existing);
                if (idx != i) Adapters.Move(idx, i);
                // Replace info but preserve toggle state
                var wasVisible = Adapters[i].IsIpVisible;
                Adapters[i] = new AdapterViewModel(fresh) { IsIpVisible = wasVisible };
            }
        }

        RaiseSummaryProperties();
        AutoSelectTestAdapter();
    }

    private void AutoSelectTestAdapter()
    {
        // Re-sync to the current instance in the list (reference may have changed after refresh)
        if (SelectedTestAdapter is not null)
        {
            var match = Adapters.FirstOrDefault(a => a.Info.InterfaceIndex == SelectedTestAdapter.Info.InterfaceIndex);
            if (match is not null)
            {
                SelectedTestAdapter = match;
                return;
            }
        }

        // Nothing selected or previous selection gone — prefer connected adapters first.
        SelectedTestAdapter =
            Adapters.FirstOrDefault(a => a.Info.IsPreferred && a.Info.IsConnected && HasUsableIpv4Address(a)) ??
            Adapters.FirstOrDefault(a => a.Info.IsConnected && HasUsableIpv4Address(a)) ??
            Adapters.FirstOrDefault(a => a.Info.IsPreferred) ??
            Adapters.FirstOrDefault();
    }

    partial void OnSelectedTestAdapterChanged(AdapterViewModel? value)
    {
        RunSpeedTestCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        RefreshCommand.NotifyCanExecuteChanged();
        SetPrimaryCommand.NotifyCanExecuteChanged();
        ElevateCommand.NotifyCanExecuteChanged();
        RunSpeedTestCommand.NotifyCanExecuteChanged();
        OnPropertyChanged(nameof(EmptyStateVisibility));
    }

    partial void OnIsSpeedTestRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(SpeedTestStatusTitle));
        OnPropertyChanged(nameof(SpeedTestStatusMessage));
        OnPropertyChanged(nameof(SpeedTestResultVisibility));
        RunSpeedTestCommand.NotifyCanExecuteChanged();
    }

    private async Task RefreshAsync()
    {
        await RunBusyActionAsync(async cancellationToken =>
        {
            ClearBanner();
            var adapters = await _adapterService.GetAdaptersAsync(cancellationToken);

            Adapters.Clear();
            foreach (var a in adapters)
                Adapters.Add(new AdapterViewModel(a));

            RaiseSummaryProperties();
            AutoSelectTestAdapter();

            if (adapters.Count == 0)
                ShowBanner("No adapters found", "Buff did not receive any IPv4 adapter data from Windows.", InfoBarSeverity.Warning);
        });
    }

    private bool CanSetPrimary(AdapterViewModel? vm) =>
        !IsBusy && vm is { Info.CanSetPrimary: true, Info.IsPreferred: false };

    private bool CanElevate() => !IsBusy && !IsAdministrator;

    private bool CanRunSpeedTest() =>
        !IsBusy &&
        !IsSpeedTestRunning &&
        SelectedTestAdapter is not null &&
        SelectedTestAdapter.Info.IsConnected &&
        SelectedTestAdapter.Info.HasDefaultRoute &&
        HasUsableIpv4Address(SelectedTestAdapter);

    private static bool HasUsableIpv4Address(AdapterViewModel? adapter) =>
        adapter is not null &&
        !string.IsNullOrWhiteSpace(adapter.Info.Ipv4Address) &&
        !string.Equals(adapter.Info.Ipv4Address, "Unavailable", StringComparison.OrdinalIgnoreCase);

    private void Elevate()
    {
        try
        {
            _privilegeService.RestartElevated();
            Application.Current.Exit();
        }
        catch (Exception ex)
        {
            if (IsElevationCanceled(ex))
            {
                return;
            }

            ShowBanner("Elevation canceled", ex.Message, InfoBarSeverity.Warning);
        }
    }

    /// <summary>Called from the system tray context menu.</summary>
    public Task SetPrimaryFromTrayAsync(AdapterViewModel vm) => SetPrimaryAsync(vm);

    private async Task SetPrimaryAsync(AdapterViewModel? vm)
    {
        if (vm is null) return;
        var adapter = vm.Info;

        if (!IsAdministrator)
        {
            try
            {
                _privilegeService.RestartElevated(adapter.InterfaceIndex);
                Application.Current.Exit();
            }
            catch (Exception ex)
            {
                if (IsElevationCanceled(ex))
                {
                    return;
                }

                ShowBanner("Elevation canceled", ex.Message, InfoBarSeverity.Warning);
            }
            return;
        }

        await RunBusyActionAsync(async cancellationToken =>
        {
            var infos = Adapters.Select(a => a.Info).ToList();
            await _priorityService.SetPrimaryAsync(adapter, infos, cancellationToken);
            ShowBanner("Primary adapter updated", $"{adapter.Name} is now the preferred connection.", InfoBarSeverity.Success);
            await SilentRefreshAsync(cancellationToken);
            // After refresh, explicitly select the newly preferred adapter
            SelectedTestAdapter = Adapters.FirstOrDefault(a => a.Info.InterfaceIndex == adapter.InterfaceIndex)
                ?? SelectedTestAdapter;
        });
    }

    private async Task RunSpeedTestAsync()
    {
        if (IsBusy || IsSpeedTestRunning) return;

        if (SelectedTestAdapter is not { } selectedAdapter ||
            !selectedAdapter.Info.IsConnected ||
            !selectedAdapter.Info.HasDefaultRoute ||
            !HasUsableIpv4Address(selectedAdapter))
        {
            ShowBanner("Adapter cannot run test", "Choose an active adapter with a default route and IPv4 address before running a speed test.", InfoBarSeverity.Warning);
            return;
        }

        LatestSpeedTestResult = null;
    SpeedTestAdapterName = string.Empty;
        CurrentSpeedPhase = SpeedTestPhase.Download;
        LiveDownloadMbps = 0;
        SpeedTestProgressPercent = 0;
        IsSpeedTestRunning = true;

        try
        {
            using var cts = new CancellationTokenSource(SpeedTestTimeout);
            var progress = new Progress<SpeedTestProgress>(snapshot =>
            {
                if (!IsSpeedTestRunning)
                {
                    return;
                }

                CurrentSpeedPhase = snapshot.Phase;
                if (snapshot.Phase == SpeedTestPhase.Download)
                {
                    LiveDownloadMbps = Math.Round(snapshot.Mbps, 2);
                    SpeedTestProgressPercent = snapshot.ProgressPercent;
                    return;
                }

                LiveDownloadMbps = Math.Round(snapshot.Mbps, 2);
                SpeedTestProgressPercent = snapshot.ProgressPercent;
            });
            SpeedTestAdapterName = selectedAdapter.Info.Name;
            var ip = selectedAdapter.Info.Ipv4Address;
            LatestSpeedTestResult = await _speedTestService.RunAsync(ip, cts.Token, progress);

            IsSpeedTestRunning = false;
            CurrentSpeedPhase = SpeedTestPhase.Download;
            LiveDownloadMbps = LatestSpeedTestResult.DownloadMbps;
            SpeedTestProgressPercent = 100;
        }
        catch (OperationCanceledException)
        {
            LatestSpeedTestResult = null;
            SpeedTestAdapterName = string.Empty;
            CurrentSpeedPhase = SpeedTestPhase.Download;
            LiveDownloadMbps = 0;
            SpeedTestProgressPercent = 0;
            ShowBanner("Speed test timed out", "The selected route did not complete the test in time. Try another connected adapter.", InfoBarSeverity.Warning);
        }
        catch (HttpRequestException ex) when (IsReachabilityFailure(ex))
        {
            // Fallback: Try speed test using system default routing instead of binding to selected adapter
            await RetrySpeedTestWithFallbackAsync(selectedAdapter, ex.Message);
        }
        catch (WebSocketException ex) when (IsReachabilityFailure(ex))
        {
            // Fallback: Try speed test using system default routing instead of binding to selected adapter
            await RetrySpeedTestWithFallbackAsync(selectedAdapter, ex.Message);
        }
        catch (Exception ex)
        {
            LatestSpeedTestResult = null;
            SpeedTestAdapterName = string.Empty;
            CurrentSpeedPhase = SpeedTestPhase.Download;
            LiveDownloadMbps = 0;
            SpeedTestProgressPercent = 0;
            ShowBanner("Speed test failed", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsSpeedTestRunning = false;
        }
    }

    private async Task RetrySpeedTestWithFallbackAsync(AdapterViewModel selectedAdapter, string originalError)
    {
        try
        {
            // Reset progress for retry
            LatestSpeedTestResult = null;
            CurrentSpeedPhase = SpeedTestPhase.Download;
            LiveDownloadMbps = 0;
            SpeedTestProgressPercent = 0;

            using var cts = new CancellationTokenSource(SpeedTestTimeout);
            var progress = new Progress<SpeedTestProgress>(snapshot =>
            {
                if (!IsSpeedTestRunning)
                {
                    return;
                }

                CurrentSpeedPhase = snapshot.Phase;
                if (snapshot.Phase == SpeedTestPhase.Download)
                {
                    LiveDownloadMbps = Math.Round(snapshot.Mbps, 2);
                    SpeedTestProgressPercent = snapshot.ProgressPercent;
                    return;
                }

                LiveDownloadMbps = Math.Round(snapshot.Mbps, 2);
                SpeedTestProgressPercent = snapshot.ProgressPercent;
            });

            // Retry without binding to specific adapter (use system default routing)
            LatestSpeedTestResult = await _speedTestService.RunAsync(null, cts.Token, progress);

            CurrentSpeedPhase = SpeedTestPhase.Download;
            LiveDownloadMbps = LatestSpeedTestResult.DownloadMbps;
            SpeedTestProgressPercent = 100;
            ShowBanner("Speed test completed (system route)", $"Switched from {selectedAdapter.Info.Name} to system default route to reach M-Lab.", InfoBarSeverity.Success);
        }
        catch (OperationCanceledException)
        {
            LatestSpeedTestResult = null;
            SpeedTestAdapterName = string.Empty;
            CurrentSpeedPhase = SpeedTestPhase.Download;
            LiveDownloadMbps = 0;
            SpeedTestProgressPercent = 0;
            ShowBanner("Speed test timed out", "Both adapter-specific and system routes failed to complete in time.", InfoBarSeverity.Warning);
        }
        catch
        {
            LatestSpeedTestResult = null;
            SpeedTestAdapterName = string.Empty;
            CurrentSpeedPhase = SpeedTestPhase.Download;
            LiveDownloadMbps = 0;
            SpeedTestProgressPercent = 0;
            ShowBanner("Adapter cannot reach M-Lab", $"{selectedAdapter.Info.Name} cannot reach M-Lab. Fallback route also failed. Check firewall/proxy settings.", InfoBarSeverity.Warning);
        }
    }

    private static bool IsReachabilityFailure(Exception ex) =>
        ex.InnerException is SocketException ||
        ex.Message.Contains("locate.measurementlab.net", StringComparison.OrdinalIgnoreCase) ||
        ex.Message.Contains("connected host has failed to respond", StringComparison.OrdinalIgnoreCase);

    private static bool IsElevationCanceled(Exception ex) =>
        ex is Win32Exception { NativeErrorCode: 1223 } ||
        ex.Message.Contains("canceled by the user", StringComparison.OrdinalIgnoreCase);

    private async Task RunBusyActionAsync(Func<CancellationToken, Task> action)
    {
        if (IsBusy) return;
        IsBusy = true;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            await action(cts.Token);
        }
        catch (Exception ex)
        {
            ShowBanner("Action failed", ex.Message, InfoBarSeverity.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void RaiseSummaryProperties()
    {
        OnPropertyChanged(nameof(AdapterCount));
        OnPropertyChanged(nameof(ConnectedAdapterCount));
        OnPropertyChanged(nameof(SummaryTitle));
        OnPropertyChanged(nameof(SummarySubtitle));
        OnPropertyChanged(nameof(EmptyStateVisibility));
        SetPrimaryCommand.NotifyCanExecuteChanged();
    }

    private void ClearBanner()
    {
        BannerTitle = string.Empty;
        BannerMessage = string.Empty;
        BannerSeverity = InfoBarSeverity.Informational;
    }

    private void ShowBanner(string title, string message, InfoBarSeverity severity)
    {
        BannerTitle = title;
        BannerMessage = message;
        BannerSeverity = severity;
    }
}
