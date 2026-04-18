using Buff_App.ViewModels;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace Buff_App;

public sealed partial class MainWindow : Window
{
    private const string WindowTitle = "Buff | Network Manager";
    private const int MinimumWindowWidth = 900;
    private const int MinimumWindowHeight = 650;
    private const int SwRestore = 9;
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
        Title = WindowTitle;
        AppWindow.Title = WindowTitle;
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

    public void BringToFront()
    {
        ShowWindow(_windowHandle, SwRestore);
        Activate();
        SetForegroundWindow(_windowHandle);
    }

    private void QuitButton_Click(object sender, RoutedEventArgs e) => DoQuit();

    private void InfoButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        InfoIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 83, 192, 40));
    }

    private void InfoButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        InfoIcon.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 138, 145, 153));
    }

    private async void InfoButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = RootGrid.XamlRoot,
            RequestedTheme = ElementTheme.Dark,
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 13, 16, 18)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 34, 38)),
            BorderThickness = new Thickness(1)
        };

        var githubButton = new HyperlinkButton
        {
            Content = "View project on GitHub",
            NavigateUri = new Uri("https://github.com/shanto-joseph/Buff"),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 83, 192, 40)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var coffeeButton = new HyperlinkButton
        {
            Content = "Buy me a coffee",
            NavigateUri = new Uri("https://coffee.shantojoseph.com/"),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 83, 192, 40)),
            BorderThickness = new Thickness(0),
            Padding = new Thickness(0),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var closeButton = new Button
        {
            Content = "Close",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0, 8, 0, 0),
            Padding = new Thickness(0, 10, 0, 10),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 83, 192, 40)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 11, 15, 14)),
            BorderThickness = new Thickness(0),
            CornerRadius = new CornerRadius(8)
        };
        closeButton.Click += (_, _) => dialog.Hide();

        var dialogContent = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 17, 20, 22)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 30, 34, 38)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20),
            Child = new StackPanel
            {
                Spacing = 10,
                Children =
                {
                    new StackPanel
                    {
                        Spacing = 4,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Buff",
                                FontSize = 22,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 237, 237, 240))
                            },
                            new TextBlock
                            {
                                Text = "Version 1.0.0",
                                FontSize = 11,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 127, 138, 146))
                            },
                            new TextBlock
                            {
                                Text = "Network adapter priority and speed testing for Windows.",
                                FontSize = 13,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 138, 145, 153)),
                                TextWrapping = TextWrapping.WrapWholeWords
                            }
                        }
                    },
                    new Border
                    {
                        Height = 1,
                        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 31, 42, 51))
                    },
                    new StackPanel
                    {
                        Spacing = 6,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Support Buff",
                                FontSize = 14,
                                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 237, 237, 240))
                            },
                            new TextBlock
                            {
                                Text = "If Buff helps your setup, you can support future updates here.",
                                FontSize = 12,
                                Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 138, 145, 153)),
                                TextWrapping = TextWrapping.WrapWholeWords
                            },
                            githubButton,
                            coffeeButton,
                            closeButton
                        }
                    }
                }
            }
        };

        dialog.Title = "About Buff";
        dialog.Content = dialogContent;

        await dialog.ShowAsync();
    }

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

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(nint hWnd);
}
