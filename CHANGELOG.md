# Changelog

All notable project changes are documented here.

## Veigar K643 Configurator v0.4.0-alpha

The RGB Studio workspace now includes an editable physical keyboard map and paint workflow.

### Added
- Minimal desktop workspace with dark and light themes and translucent glass-like panels.
- Full-size on-screen keyboard map built from individual key controls.
- Brush, fill, colour picker, eraser and key-to-LED mapping tools.
- Physical key press feedback in the keyboard map using Raw Input from the K643.
- Optional physical-key painting: after calibration, a key press applies the selected colour to its mapped LED slot and sends the updated RGB frame.
- Calibrated physical key-to-LED slot mappings are saved inside .vcp paint projects.
- Native .vcp paint project save/load and .vca animation project save/load actions.
- Dedicated paint, calibration, animation, audio and project screens.

### Changed
- Keyboard editor is shown alongside the colour inspector and layer stack.
- Key map selection, selected key feedback and colour preview update together.
- The 126-slot protocol editor remains available in the Calibration screen.

### Important hardware note
- The graphical keyboard layout is a physical-key view; the K643 HID lighting protocol exposes 126 LED slots.
- A physical key must be calibrated to its LED slot before painting that key can change the corresponding hardware LED.
- The visual keyboard-to-LED mapping and physical paint workflow require validation on the actual keyboard.
- Key events are transient. Typed text is not logged, audio is not recorded, and no telemetry is sent.

### Project formats
- .vcp: local paint project containing layers, colours, animation data and physical-key mappings.
- .vca: local animation project containing animation layers and their captured frames.
- Both formats use a readable JSON-based payload under their dedicated extensions.

### Not included yet
- Verified factory mapping for every physical key and HID LED slot.
- Ripple / neighbour-ripple until a complete physical geometry and LED map is validated.
- Macro editor and software key remapping.
- Installer and updater.

**Platform:** Windows 10/11 x64 with .NET Framework 4.8 or newer.

This is an experimental pre-release, not a stable release.

## [0.3.0-alpha] - 2026-11-08

### Fixed
- Audio peak indicator now uses a one-way WPF binding, preventing startup failure when binding to the read-only AudioPeakLevel property.
- Explicit startup error reporting and local diagnostic logs.
- Optional dark title-bar styling and Raw Input registration failures no longer block startup.
- Portable archive includes the .NET Framework 4.8 configuration and launch helper.

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
- Dedicated tabs for LED editing, layers/animation, key reactions, audio, profiles and roadmap.

### Not included yet
- Verified physical key geometry, ripple and neighbour-ripple.
- Frequency-band audio visualisation.
- Macro editor and software key remapping.
- Installer and updater.

### Notes
- v0.1.0-alpha hardware test confirmed all keys illuminated.
- v0.3.0-alpha adds control paths that require physical-device validation.
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
- RGB updates send one 382-byte HID Feature Report instead of 60 repeated reports.
- HID writes run on a background task to keep the WPF interface responsive.
- Removed automatic device refresh after every colour update.

### Notes
- Experimental pre-release; additional hardware validation required.

## [0.1.0-alpha] - 2026-11-08

### Added
- Initial WPF application and automatic device discovery.
- Basic solid-colour RGB preview commands.
- First GitHub Actions build and pre-release publishing workflow.
