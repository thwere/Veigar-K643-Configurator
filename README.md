# Veigar K643 Configurator

A minimal WPF shell for the Redragon Veigar K643 keyboard.

## Current baseline: v0.0.1

This baseline intentionally contains only the application window and the keyboard artwork exported for the project. The keyboard is currently a visual reference; physical-key input, RGB control, HID communication and key interactivity are not part of this build.

The current application source is:

`src/VeigarK643.Shell/`

The keyboard artwork is:

`assets/branding/key.png`

## Build

Requirements: Windows and the .NET 10 SDK with WPF support.

```powershell
dotnet build src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release
```

## Publish a portable Windows x64 build

```powershell
dotnet publish src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --output publish/win-x64
```

The GitHub Actions workflow builds and publishes the current `main` baseline as pre-release `v0.0.1`.

## Project status

The repository is deliberately kept on the minimal shell baseline. Older experimental RGB Studio, HID, Raw Input, profile and lighting-layer implementations were removed from the current source tree so they cannot be confused with the active project.

## License

See [LICENSE](LICENSE).
