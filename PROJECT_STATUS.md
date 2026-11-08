Copyright (C) 2026 thwere

PROJECT STATUS

Project: Veigar K643 Configurator
Current stage: Expanded RGB Studio with layers, slot editor, Raw Input mapping and audio-reactive prototype
Current target release: v0.3.0-alpha

TARGET DEVICE
Redragon Veigar K643
USB identity observed during development: VID 258A, PID 0049
Target HID collection: MI_01 / Col08
RGB report: Feature Report 0x08, 382 bytes total, 126 RGB slots

VERIFIED EXPERIMENTALLY
- Device identification over USB.
- Discovery of the target HID collection.
- Temporary RGB report transmission.
- All keyboard keys illuminated during user testing of v0.1.0-alpha.
- Physical key events previously detected using Windows Raw Input.

PRESENT IN SOURCE — v0.3.0-alpha
- Dark WPF workspace with dark Windows title bar.
- 126-slot RGB matrix editor and per-slot RGB assignment.
- HEX colour editor, RGB sliders, brightness and presets.
- Static, Gradient, Animation, AudioReactive and KeyReactive layers.
- Replace, Add and Max layer blending.
- Animation frame capture and cyclic playback.
- Raw Input filtered to the target K643 VID/PID.
- Manual physical-key-to-LED-slot calibration.
- Flash, Fade and HoldGlow key response with multiple simultaneous mappings.
- Windows Core Audio peak meter integration.
- JSON profiles with layers, frames, slot colours and key mappings.
- One Feature Report send per frame; HID I/O runs away from the UI dispatcher.

REQUIRES PHYSICAL VALIDATION
- Full per-slot rendering on the K643.
- Manual key-to-slot calibration with the new UI.
- Key-reactive effects and simultaneous key response.
- Audio-reactive lighting on the default Windows output.
- Animation playback and quick scene switching.
- Timing and responsiveness of repeated dynamic frames.

NOT YET IMPLEMENTED
- Verified physical key geometry / neighbour relationships.
- Ripple and neighbour-ripple.
- Audio spectrum / frequency-band analysis.
- Macro editor and macro playback.
- Software key remapping.
- Installer and updater.

TARGET OPERATING SYSTEMS
Windows 10 x64 is the primary target. Windows 11 x64 is also targeted but requires dedicated compatibility testing.

IMPORTANT
The 126-cell editor displays protocol LED slots and is not a verified physical keyboard layout. Per-key hardware output depends on correct manual key-to-slot calibration. No typed text is stored. Audio peak levels are sampled transiently and not recorded. No telemetry is sent and no firmware is modified.
