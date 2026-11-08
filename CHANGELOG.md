Copyright (C) 2026 thwere

CHANGELOG

This file records notable changes to Veigar K643 Configurator.
The project has not yet reached its first application release.

[Unreleased]

Initial application implementation:
- Added a four-project Visual Studio solution targeting .NET Framework 4.8.
- Added a minimal WPF application shell with device status and solid-colour RGB test controls.
- Added Windows HID interface enumeration for USB VID 258A / PID 0049.
- Added a device-specific Veigar K643 controller for the observed MI_01 / Col08 collection.
- Added the experimental 382-byte Feature Report 0x08 RGB frame builder and three-second colour preview.
- Added build and development documentation. Local compilation and hardware validation are still pending.

Repository foundation:
- Added GNU GPL-3.0-or-later license.
- Added .gitignore for Visual Studio, .NET build output and temporary files.
- Added .editorconfig for consistent source formatting.
- Added .gitattributes for consistent line endings and binary-file handling.

Research and prototype work completed before the first public application release:
- Identified the keyboard as USB VID 258A / PID 0049.
- Inspected the keyboard's HID interfaces and report descriptors.
- Successfully transmitted temporary RGB frames using a C# proof-of-concept.
- Experimentally mapped RGB slots to physical keys.
- Verified physical key detection using Windows Raw Input.

The first application prototype is now present in source. It has not yet been compiled and validated on the development PC, so it is not a stable or tested release.
