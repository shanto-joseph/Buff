using Buff_App.Services;
using Buff_App.ViewModels;
using Microsoft.Windows.AppLifecycle;
using Microsoft.UI.Xaml;

namespace Buff_App;

public partial class App : Application
{
    private const string SingleInstanceKey = "Buff.SingleInstance";
    private AppInstance? _instance;
    private MainWindow? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _instance = AppInstance.FindOrRegisterForKey(SingleInstanceKey);
        if (!_instance.IsCurrent)
        {
            var activation = AppInstance.GetCurrent().GetActivatedEventArgs();
            _instance.RedirectActivationToAsync(activation).AsTask().GetAwaiter().GetResult();
            Current.Exit();
            return;
        }

        _instance.Activated += OnRedirectedActivation;

        var adapterService = new PowerShellNetworkAdapterService();
        var priorityService = new NetworkPriorityService();
        var privilegeService = new PrivilegeService();
        var speedTestService = new MLabNdt7SpeedTestService();
        var pendingInterfaceIndex = ParsePendingInterfaceIndex(args.Arguments);
        var viewModel = new MainViewModel(adapterService, priorityService, privilegeService, speedTestService);

        _window = new MainWindow(viewModel);
        _window.Activate();
        _ = viewModel.InitializeAsync(pendingInterfaceIndex);
    }

    private void OnRedirectedActivation(object? sender, AppActivationArguments e)
    {
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _window?.BringToFront();
        });
    }

    private static int? ParsePendingInterfaceIndex(string? arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments))
        {
            return null;
        }

        var parts = arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        for (var index = 0; index < parts.Length - 1; index++)
        {
            if (!string.Equals(parts[index], "--set-primary", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (int.TryParse(parts[index + 1], out var interfaceIndex))
            {
                return interfaceIndex;
            }
        }

        return null;
    }
}
