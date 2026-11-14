# Contributing

The active application is the minimal WPF shell in `src/VeigarK643.Shell/`.

## Requirements

- Windows
- .NET 10 SDK
- WPF desktop build support

## Build

```powershell
dotnet build src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release
```

## Scope

Keep changes focused on the active shell baseline and its assets. The current baseline displays the keyboard artwork from `assets/branding/key.png`; it does not currently implement physical keyboard input, HID access or RGB control.
