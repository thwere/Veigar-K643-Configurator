Copyright (C) 2026 thwere

PROJECT STATUS

Project: Veigar K643 Configurator
Current stage: First WPF application prototype added; local build and device validation pending

TARGET DEVICE
Redragon Veigar K643
USB identity observed during development: VID 258A, PID 0049

VERIFIED EXPERIMENTALLY
- Device identification over USB.
- Discovery of multiple HID collections and inspection of HID report descriptors.
- Successful temporary RGB frame transmission using a standalone C# test utility.
- Experimental mapping of RGB slots to physical keys.
- Physical key-event detection through Windows Raw Input.

PRESENT IN SOURCE — VALIDATION PENDING
- Initial WPF application window.
- Automatic discovery of VID 258A / PID 0049 and target HID collection MI_01 / Col08.
- Solid-colour RGB preview using Feature Report 0x08.

NOT YET IMPLEMENTED
- Raw Input integration in the application.
- Key-to-LED map integration in the application.
- User-facing per-key RGB editor.
- User-facing RGB editor.
- Persistent user profiles.
- Layered lighting scenes and effect compositor.
- Frame-based animation editor.
- Audio-reactive lighting.
- User-facing macro editor and key remapping.
- Installer, updater and stable release process.

TARGET OPERATING SYSTEMS
Windows 10 x64 is the primary development environment. Windows 11 x64 is a planned target and requires dedicated compatibility testing.

IMPORTANT
Experimental protocol findings are specific to the tested keyboard and firmware configuration. A matching USB VID/PID alone does not prove compatibility with other keyboard models or firmware revisions.
