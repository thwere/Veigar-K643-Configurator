# Changelog

All notable project changes are documented here.

## [0.3.0-alpha] - 2026-11-08

### Added
- Full 126-slot RGB editor with per-slot colour assignment.
- Multi-tab dark WPF workspace and Windows dark title bar.
- Layer compositor supporting Static, Gradient, Animation, AudioReactive and KeyReactive layers.
- Replace, Add and Max blending modes with per-layer opacity.
- Animation frame capture and cyclic playback.
- Raw Input integration filtered to USB VID 258A / PID 0049.
- Manual physical key-to-LED-slot calibration.
- Key-reactive Flash, Fade and HoldGlow, including simultaneous mapped keys.
- Windows Core Audio output peak meter for audio-reactive lighting.
- Expanded JSON profiles containing layers, LED slot colours, animation frames and key mappings.
- Migration support for v0.2.0-alpha single-colour profiles.

### Changed
- Full 126-slot RGB scenes are packed into one 382-byte Feature Report.
- Audio-reactive updates are throttled to meaningful output-level changes.
- The interface now has dedicated tabs for LED editing, layers/animation, key reactions, audio, profiles and roadmap.

### Not included yet
- Verified physical key geometry, ripple and neighbour-ripple.
- Frequency-band audio visualisation.
- Macro editor and software key remapping.
- Installer and updater.

### Notes
- v0.1.0-alpha hardware test confirmed all keys illuminated.
- v0.3.0-alpha adds new control paths that require physical-device validation.
- The 126-cell grid represents protocol LED slots, not an established keyboard diagram.
- No audio is recorded or stored, typed text is not logged, and no telemetry is sent.
- Lighting remains temporary; no firmware is modified.

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
- Experimental pre-release; additional hardware validation required.

## [0.1.0-alpha] - 2026-11-08

### Added
- Initial WPF application and automatic device discovery.
- Basic solid-colour RGB preview commands.
- First GitHub Actions build and pre-release publishing workflow.
