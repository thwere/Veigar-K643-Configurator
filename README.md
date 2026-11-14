# Veigar K643 Configurator

A minimal WPF shell for the Redragon Veigar K643 keyboard.

## Current development state: `main`

The app displays a three-layer keyboard: an RGB-spectrum glow layer, the keyboard artwork, and enlarged centered mouse hitboxes. Pressing a key with the mouse lights its glow while held. Physical keyboard input is not used for highlighting. HID communication and direct RGB control of the physical keyboard are not implemented.

The `v0.0.2` pre-release introduces mouse-driven on-screen key presses and the three-layer keyboard visual.

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

The GitHub Actions workflow builds and publishes the current `main` baseline as pre-release `v0.0.1`.

## Project status

The repository is deliberately kept on the `VeigarK643.Shell` project. Older experimental RGB Studio, HID, Raw Input, profile and lighting-layer implementations remain excluded from the current source tree so they cannot be confused with the active project.

## License

See [LICENSE](LICENSE).
