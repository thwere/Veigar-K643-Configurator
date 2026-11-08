# Development Guide

Copyright (C) 2026 thwere

## Requirements

- Windows 10 x64 or Windows 11 x64.
- Visual Studio 2022 or Visual Studio Build Tools 2022.
- The **.NET desktop development** workload.
- The **.NET Framework 4.8 targeting pack**.

The solution targets .NET Framework 4.8 and uses WPF. No third-party NuGet packages are required by the initial prototype.

## Build

From the repository root, run:

```cmd
Build-VeigarConfigurator.cmd
```

The executable is produced at:

```text
src\VeigarConfigurator.App\bin\Release\VeigarConfigurator.exe
```

You can also open `VeigarConfigurator.sln` in Visual Studio and build the Release configuration.

## Initial prototype scope

The first application prototype is intentionally small:

- Enumerates Windows HID interfaces for VID 258A / PID 0049.
- Selects the observed Veigar interface MI_01 / Col08.
- Opens the HID interface using the Windows HID API.
- Sends a short solid-colour preview using Feature Report 0x08.
- Does not write keyboard configuration to persistent memory or modify firmware.

The HID frame format is based on successful experiments with the tested Veigar K643. It must not be assumed to work with other models or firmware revisions.

## Project boundaries

- `VeigarConfigurator.Core`: platform-independent interfaces and models.
- `VeigarConfigurator.Windows`: Windows HID and system API implementations.
- `VeigarConfigurator.Devices.Redragon`: Veigar-specific protocol and controller.
- `VeigarConfigurator.App`: WPF application and view models.

Keep hardware-specific packet structures inside the device project. Keep Win32 interop inside the Windows project. The WPF application should depend on abstractions rather than packet details.

## Safety and testing

Close other keyboard configuration software before running HID tests. The RGB preview temporarily changes the visible lighting and sends repeated frames for approximately three seconds. The current prototype does not save settings to the keyboard.
