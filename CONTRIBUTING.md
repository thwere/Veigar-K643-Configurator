# Contributing

The active application is the WPF editor in `src/VeigarK643.Shell/`. The separate layout utility is in `tools/VeigarK643.LayoutEditor/`.

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

The active shell currently has a large WPF window class that coordinates UI state, profile storage, and HID output. Refactoring these responsibilities into separate services is a planned engineering task; it should be done incrementally with regression tests rather than as a broad rewrite.
