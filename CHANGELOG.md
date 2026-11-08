# Changelog

All notable project changes are documented here.

## [0.2.0-alpha] - 2026-11-08

### Added
- RGB studio with editable red, green and blue channels.
- Six-digit HEX colour input.
- Brightness control with live output preview.
- Eight quick colour presets plus a dedicated lights-off command.
- Local JSON lighting profiles with save and load dialogs.
- Elapsed-time reporting for each hardware update.

### Changed
- RGB updates now send one 382-byte HID Feature Report instead of 60 repeated reports.
- HID writes run on a background task to keep the WPF interface responsive.
- Removed the automatic device refresh after every colour update.

### Notes
- This is an experimental pre-release for the Redragon Veigar K643.
- Physical validation of the new editor and profile workflow is still required.
- Profiles store editor values locally; applying a profile to the keyboard remains an explicit action.
- Lighting settings are temporary and no firmware is modified.

## [0.1.0-alpha] - 2026-11-08

### Added
- Initial WPF application and automatic device discovery.
- Basic solid-colour RGB preview commands.
- First GitHub Actions build and pre-release publishing workflow.

### Notes
- The initial release was intended for first hardware validation.
