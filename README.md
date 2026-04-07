# Xbox Controller Studio

Open-source Windows desktop app (C# + WPF + MVVM) for Xbox controller monitoring, profile-based mapping, and low-latency input-to-keyboard/mouse actions.

## What This Project Does

Xbox Controller Studio is built to be a practical control center for Xbox controllers on Windows 10/11. It focuses on real-time input visibility, customizable mapping profiles, and a clean desktop experience suitable for gaming and productivity workflows.

## Key Features

- Real-time controller polling using XInput.
- Live dashboard showing connection status and battery information.
- Deadzone tuning and sensitivity controls.
- Button mapping from controller input to keyboard keys and mouse buttons.
- Optional right-stick-to-mouse movement with adjustable sensitivity.
- Profile management with per-game target executable support.
- Runtime localization support (English and Brazilian Portuguese).
- System tray integration (minimize to tray, quick open/exit).
- Low battery notifications with configurable threshold.

## Tech Stack

- .NET 8
- WPF
- MVVM architecture
- Win32 APIs (XInput and SendInput)

## Project Structure

- `Views` - WPF views and visual layout
- `ViewModels` - UI state, commands, interaction logic
- `Models` - domain/data models
- `Services` - integrations (XInput, SendInput, localization, polling)
- `Core` - reusable logic and infrastructure helpers

## Requirements

- Windows 10 or Windows 11
- .NET SDK 8.0+

## Getting Started

```powershell
dotnet restore
dotnet build
dotnet run
```

## Build Executable (Local)

To generate a distributable executable for Windows x64:

```powershell
dotnet publish XboxControllerStudio.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true /p:PublishTrimmed=false -o publish/win-x64
```

The output will be available in:

- `publish/win-x64`

You can zip this folder and share it with end users.

## GitHub Releases (Automatic)

This repository includes a GitHub Actions workflow at `.github/workflows/release.yml` that:

- builds the app on Windows
- publishes a self-contained win-x64 executable
- generates `XboxControllerStudio-win-x64.zip`
- creates a GitHub Release and uploads the zip file

To publish a new release:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

Use semantic version tags (`vMAJOR.MINOR.PATCH`) like `v1.0.0`.

## Architecture Notes

The project follows MVVM with clear separation of concerns:

- UI and visual behavior in `Views`
- UI state and commands in `ViewModels`
- Input integrations and external APIs in `Services`
- Shared processing logic in `Core`

## Current Limitations

- XInput is the primary controller API.
- No virtual controller emulation layer is included.
- Some Bluetooth battery scenarios rely on best-effort fallbacks.

## Open Source Roadmap

- Improve profile import/export and sharing.
- Add automated tests for core input/mapping flows.
- Expand localization coverage and contributor docs.
- Provide packaged releases and installer workflow.
