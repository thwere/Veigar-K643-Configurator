# Contributing

The active application is the WPF editor in `src/VeigarK643.Shell/`. The separate layout utility is in `tools/VeigarK643.LayoutEditor/`.

The main window is split across partial-class files by responsibility. Preserve existing UI behavior and profile JSON compatibility when moving code. Keep refactors small and validate them with a successful build and regression checks.

## Requirements

- Windows
- .NET 10 SDK
- WPF desktop build support

## Build and validate

```powershell
dotnet restore src/VeigarK643.Shell/VeigarK643.Shell.csproj
dotnet build src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --no-restore
dotnet publish src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --output publish/win-x64
```

The GitHub Actions build also publishes the layout editor as a portable artifact and runs NuGet dependency auditing.

## Change-safety rules

- Preserve existing user workflows and the on-disk profile format unless a migration is explicitly designed.
- Validate imported JSON before using it. Treat files selected by the user as untrusted input.
- Keep profile writes failure-safe; do not replace a valid profile with partially written data.
- Do not change HID report headers, report length, VID/PID/interface matching, or LED-slot assignments without a documented hardware verification.
- Keep new dependencies to a minimum and review their licenses and security advisories.
- Do not commit local profile data, credentials, personal files, build output, or device-specific logs.
- When changing editor behavior, describe the behavior change and test it on Windows and the physical keyboard.

## Architecture

The WPF window is split across `MainWindow.Editor.cs`, `MainWindow.ColorEditor.cs`, `MainWindow.Groups.cs`, `MainWindow.ProfileStorage.cs`, `MainWindow.Profiles.cs`, and `MainWindow.WindowChrome.cs`. Shared snapshots and profile/layout models are in `EditorModels.cs`. These files are partial declarations of the same window class, so private state and XAML event wiring remain unchanged while the code is easier to navigate.

Further extraction into independent services (for example, profile storage and group state) should be incremental and supported by automated regression tests. Avoid broad rewrites of the HID protocol or profile JSON schema.
