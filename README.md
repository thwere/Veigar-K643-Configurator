# Veigar K643 Configurator

A Windows WPF editor for the Redragon Veigar K643 keyboard.

## Current capabilities

- Brush, fill, eyedropper, selection, and eraser tools for the virtual keyboard.
- HSV-style color palette, HEX input, and recently used colors.
- Named keyboard groups with editable key membership and drag-and-drop priority ordering. Higher groups take precedence when group memberships overlap.
- Independent local profiles with names, colors, drag-and-drop startup priority, JSON import, and export.
- Undo/redo history for editor operations.
- HID Feature Report output to the supported Veigar K643 interface, including reconnect handling.
- A separate layout editor utility for adjusting keyboard hitbox geometry.

The keyboard layout is embedded from `assets/layout/veigar-k643-layout.json`. Its coordinates use the 599×222 artwork space. The current HID mapping is maintained in `src/VeigarK643.Shell/VeigarK643HidController.cs`.

## Version status

The current development line is **v0.4.0 (pre-release)**. Versions below 1.0.0 are intended for testing and iterative development.

## Source structure

The active WPF window is split into partial-class files by responsibility: editor interaction, color editing, groups, profile storage, profile UI, and window chrome. Shared data models are in `EditorModels.cs`. This is a source-organization refactor; runtime behavior, HID communication, and the existing profile JSON format are retained.

## Build

Requirements: Windows and the .NET 10 SDK with WPF support.

```powershell
dotnet restore src/VeigarK643.Shell/VeigarK643.Shell.csproj
dotnet build src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --no-restore
```

## Portable Windows x64 build

```powershell
dotnet publish src/VeigarK643.Shell/VeigarK643.Shell.csproj --configuration Release --runtime win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true --output publish/win-x64
```

GitHub Actions builds the projects on pushes and pull requests. A push to `main` builds and refreshes the pre-release using the version declared in the active shell project file.

## License

See [LICENSE](LICENSE).
