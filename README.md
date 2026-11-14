# Veigar K643 Configurator

A WPF editor for the Redragon Veigar K643 keyboard.

## Current editor capabilities

- Brush: paint multiple keys by dragging across the virtual keyboard.
- Fill: apply the current color to selected keys or the entire keyboard.
- Selection: click, Shift-toggle, and marquee-select keys.
- Groups: global group manager with names, colors, key membership, and stored state (static, animation, or scenario). Clicking a grouped key selects the whole group.
- Eraser: remove lighting by dragging across keys.
- Color picker: standard red-to-red hue field with black value gradient, a vertical saturation slider, and live preview.
- Hardware output: editor changes are sent through the HID Feature Report interface; startup and USB reconnects trigger up to 10 resend attempts, with HID errors shown in the status bar.
- Local profiles: save key colors and groups as JSON files in `%USERPROFILE%\Documents\VeigarConfig\Profiles`.
- Interface: fixed-size editor window, revised tool icons, a wider parameter panel, and contextual hints in the bottom status bar.
- Profiles: profile color/name selector, per-profile settings and deletion confirmation, protected final profile, JSON import/export.
- History: Ctrl+Z / Ctrl+Y with a 250-operation undo/redo history, including key painting, erasing, filling, and group edits.
- Groups: edit group membership by adding or removing individual keys.

The keyboard layout is loaded from `assets/layout/veigar-k643-layout.json` in the 599×222 artwork coordinate space.

## Build

Requirements: Windows and the .NET 10 SDK with WPF support.

```powershell
dotnet build src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release
```

## Portable Windows x64 build

```powershell
dotnet publish src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --output publish/win-x64
```

GitHub Actions builds the application on pushes to `main`. Pre-releases are updated by commits whose message begins with `release-v`.

## License

See [LICENSE](LICENSE).
