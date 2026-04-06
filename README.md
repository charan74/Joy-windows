## 🤖 Joy — AI Focus Companion for Windows

> A privacy-first, system-tray focus companion that uses real-time webcam-based eye tracking to enforce deep work through expressive pixel-art feedback, behavioral state transitions, and retro audio cues.

---

## 🎥 Preview

![Demo](assets/demo.gif)

---

## 🧠 The Idea

Most productivity tools rely on reminders and discipline.
**Joy takes a different approach — it enforces focus through feedback.**

Instead of telling you to focus, Joy reacts to your behavior:

* 👀 Stay focused → calm, happy robot
* 😐 Look away → subtle warning
* 😠 Stay distracted → escalating frustration + alerts

This creates a real-time behavioral loop that nudges you back into deep work.

---

## ✨ Features

* 👁 Real-time face & eye detection (OpenCV, fully offline)
* 🤖 Pixel-art emotional robot UI (custom WPF rendering)
* 🔁 Behavioral state machine for distraction escalation
* 🔔 System tray integration with notifications
* 🔊 Retro sound synthesis (NAudio)
* ⏱ Custom focus sessions (countdown + control)
* ⚡ Lightweight overlay, always accessible
* 🔒 Privacy-first (no internet, no data collection)

---

## 🏗 Architecture

```
Joy.Windows/
├── Models/
│   ├── Mood.cs
│   ├── DistractionPhase.cs
│   └── TimerState.cs
│
├── ViewModels/
│   └── MainViewModel.cs
│
├── Services/
│   ├── CameraService.cs
│   ├── FocusDetectorService.cs
│   ├── FocusMoodController.cs
│   ├── SoundService.cs
│   └── NotificationService.cs
│
├── Controls/
│   └── RobotEyesControl.cs
│
├── Views/
│   ├── MainWindow.xaml
│   └── TimerSetupWindow.xaml
│
├── Resources/
│   └── VT323-Regular.ttf
│
├── App.xaml / App.xaml.cs
└── Joy.Windows.csproj
```

---

## ⚙️ Tech Stack

| Layer              | Technology                    |
| ------------------ | ----------------------------- |
| UI                 | WPF (.NET 8)                  |
| Architecture       | MVVM (CommunityToolkit.Mvvm)  |
| Computer Vision    | OpenCV (OpenCvSharp4.Windows) |
| Audio              | NAudio                        |
| System Integration | Hardcodet.NotifyIcon.Wpf      |
| Language           | C#                            |

---

## 🚀 Getting Started

### Requirements

* Windows 10 (1903+) / Windows 11
* .NET 8 SDK
* Webcam

---

### Build & Run

```powershell
# Restore dependencies
dotnet restore

# Run in debug
dotnet run --project Joy.Windows

# Publish self-contained EXE
dotnet publish Joy.Windows -c Release -r win-x64 --self-contained -o publish/
```

---

## 🧠 How It Works

1. Camera captures frames in real time
2. Face & eye detection determines attention
3. A behavioral state machine evaluates focus
4. Robot emotion updates dynamically
5. Sound and notifications reinforce behavior

---

## 🔄 Behavioral Model

```
Focused → Neutral → Distracted → Warning → Angry → Alert
```

Each stage increases intensity to bring you back to focus.

---

## 🔒 Privacy First

* No cloud APIs
* No tracking
* No data storage
* Fully offline

All processing happens locally on your device.

---

## 🎯 Use Cases

* Deep work sessions
* Studying / exam preparation
* Coding & productivity blocks
* Building discipline habits

---

## 🤝 Contributing

Pull requests are welcome. For major changes, please open an issue first.

---

## 📄 License

MIT License

---

## 🌟 Vision

Joy is a behavioral interface for attention.

The long-term goal is to evolve into:

* A personal focus OS
* A quantified-self productivity system
* A real-time cognitive assistant

---

## ⭐ Support

If you like this project:

* Star the repo
* Fork it
* Share feedback

---

# 🚀 Built for focus. Designed for behavior.

---

