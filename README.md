# Veigar K643 Configurator

A minimal WPF shell for the Redragon Veigar K643 keyboard.

## Current development state: `main`

The app displays the keyboard artwork and highlights pressed keys while the application window is active. This preview uses normal WPF keyboard events: it does not capture input globally and does not use Raw Input. HID communication and RGB control are not implemented.

The `v0.0.1` pre-release remains the original minimal shell build and does not include this key-press preview.

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

The repository is deliberately kept on the `VeigarK643.Shell` project. Older experimental RGB Studio, HID, Raw Input, profile and lighting-layer implementations remain excluded from the current source tree so they cannot be confused with the active project.

## License

See [LICENSE](LICENSE).
