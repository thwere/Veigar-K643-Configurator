# Architecture Overview

Copyright (C) 2026 thwere

## Dependency direction

```text
VeigarConfigurator.App
    |-- VeigarConfigurator.Core
    |-- VeigarConfigurator.Windows
    |-- VeigarConfigurator.Devices.Redragon
              |
              +-- VeigarConfigurator.Core

VeigarConfigurator.Windows
    +-- VeigarConfigurator.Core
```

The Core project defines contracts. Windows provides Win32/HID implementations. The Redragon device project implements the K643-specific packet protocol. The WPF application composes these services and presents their state to the user.

## Device discovery

The Windows HID enumerator uses SetupAPI and the HID device class GUID. It filters interface paths by VID 258A / PID 0049 and extracts MI and COL identifiers from the path. The Veigar controller currently requires MI_01 / Col08, matching the interface used by the successful proof-of-concept.

## RGB transport

The current preview uses HID Feature Report 0x08, 382 bytes total, including the Report ID. It sends a 4-byte observed header followed by 126 RGB slots (3 bytes per slot). A solid-colour preview sends 60 frames at 50 ms intervals.

This packet layout is an experimental device-specific finding, not a general Redragon protocol specification.

## Next implementation steps

1. Build and run the WPF prototype on the development PC.
2. Confirm device enumeration selects the expected HID path.
3. Test red, green, blue and off previews.
4. Add unit tests for RGB frame generation and HID path parsing.
5. Move Raw Input key detection into a dedicated Windows input service.
6. Import and verify the experimentally created key-to-LED map.
