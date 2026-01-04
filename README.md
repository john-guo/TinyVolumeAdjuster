# TinyVolumeAdjuster

TinyVolumeAdjuster is a **lightweight Windows desktop tool** that allows you to adjust the **system master volume** with **much finer granularity** than the default Windows volume steps.

It is designed as a small, fast, and practical utility for precise volume control.

---

## Features

- 🎚 **Fine-grained volume adjustment**
  - Smaller step size than the default Windows volume control
- 🖥 **WPF desktop application**
  - Fast startup, low resource usage
- 🔊 **Uses Windows Core Audio API**
  - Directly controls the system master volume
- 🧠 **MVVM architecture**
  - Clean structure, easy to maintain or extend
- 🛡 **Basic exception handling**
  - Prevents common unhandled exceptions from crashing the application

---

## User Interface

After launching the application, you will see a simple and minimal window:

- **Volume display** showing the current system master volume
- **Controls** (buttons or slider) for fine adjustment
- **Immediate effect** — all changes apply instantly

> This application controls the **system master volume only**.  
> Per-application volume control is not supported.

---

## How to Use

### 1. Run the application

- Run the compiled `TinyVolumeAdjuster.exe`, or
- Open the solution in Visual Studio and press `F5`

No administrator privileges are required.

---

### 2. Adjust the volume

- Use the UI controls to increase or decrease volume
- Each adjustment is applied immediately
- Suitable for scenarios such as:
  - Music listening
  - Video playback
  - Low-volume environments (night use)
  - Precise audio level tuning

---

## System Requirements

- Windows 10 or Windows 11
- .NET Desktop Runtime (matching the target framework)
- An audio device that supports Windows Core Audio

---

## Build & Development

### Open the project

1. Clone the repository:

```bash
git clone https://github.com/john-guo/TinyVolumeAdjuster.git
```

2. Open the solution using Visual Studio 2022 or later

3. Restore NuGet packages (if required)

4. Build and run the project

---

## Project Structure

	TinyVolumeAdjuster/
	├── App.xaml                # WPF application entry point
	├── MainWindow.xaml         # Main window UI
	├── MainViewModel.cs        # MVVM ViewModel
	├── VolumeAdjuster.cs       # Core system volume control logic
	├── HandleBug.cs            # Exception / error handling
	├── Utils.cs                # Common utility methods
	└── TinyVolumeAdjuster.csproj


## Notes

- Windows-only application

- Controls system master volume

- Does not modify system settings or registry

- Does not run in the background (exits when the window is closed)

## Use Cases

- Default Windows volume steps are too large

- Precise audio control is required

- Headphone or monitoring environments

- A simple, portable, no-install utility for volume adjustment


## License

This project is a personal utility tool.
Please ensure proper compliance if you plan to redistribute or modify it.