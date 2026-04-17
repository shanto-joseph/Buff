using Buff_App.ViewModels;
using H.NotifyIcon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Graphics;
using Windows.UI;

namespace Buff_App;

public sealed partial class MainWindow : Window
{
    private const int MinimumWindowWidth = 900;
    private const int MinimumWindowHeight = 640;
    private const int GwlWndProc = -4;
    private const uint WmGetMinMaxInfo = 0x0024;

    private TaskbarIcon? _trayIcon;
    private bool _reallyClosing;
    private readonly DispatcherQueue _dispatcher;
    private readonly nint _windowHandle;
    private nint _previousWndProc;
    private WndProcDelegate? _wndProcDelegate;

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        RootGrid.DataContext = ViewModel;
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

        _dispatcher = DispatcherQueue.GetForCurrentThread();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/tray.ico");
        AppWindow.Resize(new SizeInt32(1200, 820));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = false;
            presenter.SetBorderAndTitleBar(true, false);
        }

        HookMinimumWindowSize();

        SetupTrayIcon();

        // Wire XamlRoot to the tray flyout once the window content is loaded
        RootGrid.Loaded += (_, _) =>
        {
            if (_trayIcon?.ContextFlyout is MenuFlyout flyout)
                flyout.XamlRoot = RootGrid.XamlRoot;
        };

        AppWindow.Closing += (_, args) =>
        {
            if (_reallyClosing) return;
            args.Cancel = true;
            AppWindow.Hide();
        };
    }

    public MainViewModel ViewModel { get; }

    private void DoQuit()
    {
        _reallyClosing = true;
        ViewModel.StopPolling();
        _trayIcon?.Dispose();
        _trayIcon = null;
        Application.Current.Exit();
    }

    private void SetupTrayIcon()
    {
        try
        {
            _trayIcon = new TaskbarIcon
            {
                ToolTipText = "Buff",
                ContextMenuMode = ContextMenuMode.PopupMenu
            };

            var iconPath = System.IO.Path.Combine(
                System.IO.Path.GetDirectoryName(Environment.ProcessPath) ?? string.Empty,
                "Assets", "tray.ico");
            if (System.IO.File.Exists(iconPath))
                _trayIcon.Icon = new System.Drawing.Icon(iconPath);

            var menu = new MenuFlyout();

            var header = new MenuFlyoutItem
            {
                Text = "Buff",
                FontSize = 15,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 83, 192, 40))
            };
            header.Click += (_, _) => _dispatcher.TryEnqueue(() => AppWindow.Show());

            var quitItem = new MenuFlyoutItem
            {
                Text = "Quit Buff",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromArgb(255, 220, 80, 60))
            };
            quitItem.Click += (_, _) => _dispatcher.TryEnqueue(() => DoQuit());

            menu.Items.Add(header);
            menu.Items.Add(new MenuFlyoutSeparator());
            menu.Items.Add(quitItem);

            _trayIcon.ContextFlyout = menu;
            _trayIcon.ForceCreate();
        }
        catch
        {
            // Tray icon is optional
        }
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e) => DoQuit();

    private void HookMinimumWindowSize()
    {
        _wndProcDelegate = WindowProc;
        var newWndProc = Marshal.GetFunctionPointerForDelegate(_wndProcDelegate);
        _previousWndProc = SetWindowLongPtr(_windowHandle, GwlWndProc, newWndProc);
    }

    private nint WindowProc(nint hWnd, uint msg, nuint wParam, nint lParam)
    {
        if (msg == WmGetMinMaxInfo)
        {
            var minMaxInfo = Marshal.PtrToStructure<MinMaxInfo>(lParam);
            minMaxInfo.PointMinTrackSize.X = MinimumWindowWidth;
            minMaxInfo.PointMinTrackSize.Y = MinimumWindowHeight;
            Marshal.StructureToPtr(minMaxInfo, lParam, false);
            return 0;
        }

        return CallWindowProc(_previousWndProc, hWnd, msg, wParam, lParam);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point PointReserved;
        public Point PointMaxSize;
        public Point PointMaxPosition;
        public Point PointMinTrackSize;
        public Point PointMaxTrackSize;
    }

    private delegate nint WndProcDelegate(nint hWnd, uint msg, nuint wParam, nint lParam);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "CallWindowProcW", SetLastError = true)]
    private static extern nint CallWindowProc(nint lpPrevWndFunc, nint hWnd, uint msg, nuint wParam, nint lParam);
}
