Copyright (C) 2026 thwere

PROJECT STATUS

Project: Veigar K643 Configurator
Current stage: RGB Studio prototype; v0.2.0-alpha prepared for expanded hardware testing

TARGET DEVICE
Redragon Veigar K643
USB identity observed during development: VID 258A, PID 0049
Target HID collection: MI_01 / Col08

VERIFIED EXPERIMENTALLY
- Device identification over USB.
- Discovery of multiple HID collections and inspection of HID report descriptors.
- Successful temporary RGB frame transmission using a standalone C# test utility.
- Successful lighting of all keyboard keys using the initial WPF application.
- Experimental mapping of RGB slots to physical keys.
- Physical key-event detection through Windows Raw Input.

PRESENT IN SOURCE — v0.2.0-alpha VALIDATION IN PROGRESS
- WPF RGB Studio with red, green and blue channel sliders.
- Six-digit HEX colour entry.
- Brightness adjustment and output preview.
- Eight quick colour presets and lights-off command.
- Local JSON profile save/load.
- One-shot 382-byte HID Feature Report transmission on a background task.
- Device discovery for VID 258A / PID 0049 and HID collection MI_01 / Col08.

NOT YET IMPLEMENTED
- Integrated Raw Input key detection.
- Verified physical key-to-LED map in the application.
- User-facing per-key RGB keyboard editor.
- Persistent profile library and profile switching.
- Layered lighting scenes and effect compositor.
- Frame-based animation editor.
- Audio-reactive lighting.
- Keypress-reactive lighting and ripple effects.
- User-facing macro editor and key remapping.
- Installer and updater.

TARGET OPERATING SYSTEMS
Windows 10 x64 is the primary development environment. Windows 11 x64 is a planned target and requires dedicated compatibility testing.

IMPORTANT
The current release controls global solid-colour lighting only. The experimental LED-slot mapping has not yet been integrated into this UI. The new one-report update path must be validated on physical hardware. Experimental protocol findings are specific to the tested keyboard and firmware configuration.
