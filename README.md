# Veigar K643 Configurator

A minimal WPF shell for the Redragon Veigar K643 keyboard.

## Current development state: `main`

The app displays a three-layer keyboard: an RGB-spectrum glow layer, the keyboard artwork, and invisible mouse hitboxes. Pressing a key with the mouse lights its glow while held. Physical keyboard input is not used for highlighting. On-screen mouse presses are sent to the keyboard through its HID Feature Report interface using the measured 126-slot LED map.

The `v0.0.3` checkpoint hides hitboxes, uses a fixed-size non-resizable window, and adds HID RGB feedback for virtual key presses.

The current application source is:

`src/VeigarK643.Shell/`

The keyboard artwork is:

`assets/branding/key.png`

Key highlight regions are loaded from `assets/layout/veigar-k643-layout.json` in the 599×222 artwork coordinate space.

## Build

Requirements: Windows and the .NET 10 SDK with WPF support.

```powershell
dotnet build src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release
```

## Publish a portable Windows x64 build

```powershell
dotnet publish src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --output publish/win-x64
```

The Windows build runs on pushes to `main`. The portable pre-release workflow is started manually or by pushing a version tag.

## Project status

The repository is deliberately kept on the `VeigarK643.Shell` project. Older experimental RGB Studio, Raw Input, profile and lighting-layer implementations remain excluded from the current source tree so they cannot be confused with the active project.

## License

See [LICENSE](LICENSE).
