# Joy — Windows

A focus companion that lives in your system tray.  
Pixel-art robot eyes watch you through the camera — stay focused and they're happy, look away and they get angry.

## Requirements

- Windows 10 version 1903+ / Windows 11
- .NET 8.0 SDK
- Webcam

## Build & Run

```powershell
# Restore dependencies
dotnet restore

# Run in debug
dotnet run --project Joy.Windows

# Publish self-contained EXE
dotnet publish Joy.Windows -c Release -r win-x64 --self-contained -o publish/
```

## Dependencies

| Package | Purpose |
|---------|---------|
| `OpenCvSharp4.Windows` | Camera capture + face detection |
| `NAudio` | Retro sound synthesis |
| `Hardcodet.NotifyIcon.Wpf` | System-tray icon & balloon notifications |
| `CommunityToolkit.Mvvm` | ObservableObject / RelayCommand |

## Architecture

```
Joy.Windows/
├── Models/
│   ├── Mood.cs                  # Eye mood enum
│   ├── DistractionPhase.cs      # Escalation phases enum
│   └── TimerState.cs            # Countdown / stopwatch state
├── ViewModels/
│   └── MainViewModel.cs         # Main MVVM view model
├── Services/
│   ├── CameraService.cs         # OpenCV camera capture
│   ├── FocusDetectorService.cs  # Haar-cascade face+eye detection
│   ├── FocusMoodController.cs   # Distraction state machine
│   ├── SoundService.cs          # NAudio square-wave synthesis
│   └── NotificationService.cs  # System tray notifications
├── Controls/
│   └── RobotEyesControl.cs      # WPF DrawingContext eye renderer
├── Views/
│   ├── MainWindow.xaml/.cs      # Floating overlay window
│   └── TimerSetupWindow.xaml/.cs# Duration picker popup
├── Resources/
│   └── VT323-Regular.ttf
├── App.xaml / App.xaml.cs
└── Joy.Windows.csproj
```

## License

MIT
