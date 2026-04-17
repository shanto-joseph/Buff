using Buff_App.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Buff_App;

public sealed partial class MainWindow : Window
{
    private const int MinimumWindowWidth = 900;
    private const int MinimumWindowHeight = 650;
    private const int GwlWndProc = -4;
    private const uint WmGetMinMaxInfo = 0x0024;

    private readonly nint _windowHandle;
    private nint _previousWndProc;
    private WndProcDelegate? _wndProcDelegate;

    public MainWindow(MainViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        RootGrid.DataContext = ViewModel;
        _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/tray.ico");
        AppWindow.Resize(new SizeInt32(1200, 830));

        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
            presenter.IsAlwaysOnTop = false;
            presenter.SetBorderAndTitleBar(true, false);
        }

        HookMinimumWindowSize();
    }

    public MainViewModel ViewModel { get; }

    private void DoQuit()
    {
        ViewModel.StopPolling();
        Application.Current.Exit();
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
