using Buff_App.Models;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Buff_App.ViewModels;

public sealed partial class AdapterViewModel : ObservableObject
{
    public AdapterViewModel(NetworkAdapterInfo info)
    {
        Info = info;
        ToggleIpCommand = new RelayCommand(() => IsIpVisible = !IsIpVisible);
    }

    public NetworkAdapterInfo Info { get; }

    [ObservableProperty]
    public partial bool IsIpVisible { get; set; }

    public string IpDisplay => IsIpVisible ? Info.Ipv4Address : "••••••••••";

    public string EyeGlyph => IsIpVisible ? "\uED1A" : "\uE7B3";

    public IRelayCommand ToggleIpCommand { get; }

    partial void OnIsIpVisibleChanged(bool value)
    {
        OnPropertyChanged(nameof(IpDisplay));
        OnPropertyChanged(nameof(EyeGlyph));
    }
}
