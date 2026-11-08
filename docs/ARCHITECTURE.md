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

Core defines device contracts. Windows provides Win32/HID, Raw Input and Core Audio integrations. The Redragon project owns K643-specific protocol framing and device control. The WPF application composes these services, manages profiles and renders lighting scenes.

## Device discovery

The HID enumerator uses SetupAPI and the HID device class GUID. It filters interface paths by VID 258A / PID 0049 and extracts MI and COL identifiers. The controller requires MI_01 / Col08, matching the interface used by the successful proof-of-concept.

## RGB transport

The device-specific Feature Report is 382 bytes total: Report ID 0x08, a four-byte observed header and 126 RGB slots (three bytes per slot). A full scene is packed into one report. HID writes are synchronous at the Win32 boundary and therefore executed on a worker task rather than the WPF dispatcher.

This packet layout is an experimental finding for the tested K643, not a general Redragon protocol specification.

## Lighting pipeline

```text
Profile / Editor
      |
      v
Lighting Layers
      |
      v
Layer Compositor
  | Static
  | Gradient
  | Animation
  | AudioReactive
  | KeyReactive
      |
      v
126 x RGB byte slots
      |
      v
VeigarRgbFrameBuilder
      |
      v
HidD_SetFeature (Report 0x08)
```

Layers are composited bottom-to-top with Replace, Add or Max blending and per-layer opacity. The animation layer cycles its stored frames. The audio layer scales its base colour using the default Windows output endpoint peak meter. The key-reactive layer uses transient key-to-slot events.

## Key input and calibration

The Raw Input service registers a keyboard usage collection and filters incoming device paths by VID 258A / PID 0049. It emits virtual-key and scan-code events only; it does not capture text, suppress keyboard input or persist key events.

The 126-cell matrix is a protocol LED-slot editor, not a physical keyboard geometry map. The user can calibrate each slot by selecting it and pressing the corresponding physical key. The resulting key-to-slot association is stored in the local profile. Spatial neighbour effects remain disabled until physical geometry is verified.

## Audio integration

The Windows Core Audio peak meter reads the default multimedia output endpoint. Audio values are sampled transiently for lighting intensity; the application does not record audio buffers or store samples.

## Profiles

JSON profiles store layer definitions, opacity/blend settings, 126-slot colours, animation frames and calibrated key mappings. The loader migrates the earlier v0.2.0-alpha solid-colour profile format.

## Deferred work

Macro playback and software key remapping are separate future modules. Ripple/neighbour-ripple requires a verified physical key geometry. Frequency-band audio analysis requires a dedicated audio capture/FFT implementation.
