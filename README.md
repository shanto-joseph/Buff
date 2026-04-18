# Buff

Buff is an open-source WinUI 3 desktop app for managing Windows network adapter priority and testing real-time internet performance.

## Overview

Buff helps you:

- Discover active network adapters and inspect their effective route metrics
- Choose a preferred adapter by applying IPv4 interface metric priority
- Track adapter status changes continuously
- Run a native M-Lab NDT7 speed test (download, upload, ping, jitter) directly in the app

## Screenshot

Screenshot will be added soon.

## Features

- Native WinUI 3 desktop experience
- Live adapter monitoring with route details
- One-click preferred adapter switching
- In-app speed testing (no external speedtest executable required)
- MVVM architecture for maintainability

## Tech Stack

- .NET 10 (`net10.0-windows10.0.26100.0`)
- WinUI 3 / Windows App SDK
- CommunityToolkit.Mvvm
- PowerShell-backed networking services

## Project Structure

```text
.
|- Models/                  # Domain and data models
|- ViewModels/              # View state and commands
|- Services/                # Adapter, privilege, and speed-test services
|- Converters/              # XAML converters
|- Assets/                  # Icons and fonts
|- Tools/Speedtest/         # Legacy notes (native test is now in-app)
|- MainWindow.xaml          # Main UI
|- MainWindow.xaml.cs       # Window behavior
|- App.xaml.cs              # App startup/composition
|- Buff.App.csproj          # Project settings
`- buff.sln                 # Solution file
```

## Requirements

- Windows 10/11
- .NET 10 SDK
- Visual Studio 2022 or newer with Windows desktop development workload





## Usage Notes

- Adapter refresh runs in the background.
- Setting preferred adapter priority requires administrator privileges.
- Speed tests use M-Lab NDT7 over secure WebSockets.

## Troubleshooting

### No adapters appear

- Ensure adapters are enabled and not blocked by policy.
- Run the app as administrator.

### Cannot set preferred adapter

- Confirm the app is running with elevated privileges.

### Speed test fails

- Check internet connectivity.
- Confirm firewall/proxy settings allow outbound secure WebSocket traffic.

## Contributing

Contributions are welcome.

1. Fork the repository.
2. Create a feature branch.
3. Commit your changes.
4. Open a pull request.

## Support

If this project helps you, you can support development here:

[![Buy Me A Coffee](https://img.shields.io/badge/Buy%20Me%20a%20Coffee-Support-ffdd00?logo=buymeacoffee&logoColor=000000)](https://coffee.shantojoseph.com/)



## License

This project is licensed under the MIT License. See [LICENSE](LICENSE).
