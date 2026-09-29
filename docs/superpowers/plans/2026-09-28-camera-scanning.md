# Camera Scanning Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task.

**Goal:** The headline requirement — point the phone at a printed barcode, have it read and displayed, and
have a link offered when the payload is one.

**Architecture:** `BarcodeScanning.Native.Maui` supplies the camera preview control and the platform
decoder (Google ML Kit on Android, Apple Vision on iOS). Our own `ScanCoordinator` sits between the engine
and the UI: it maps engine formats onto the generator's symbology vocabulary, filters the repeats that a
continuous scan produces, interprets the payload and records the result. The engine is touched in exactly
two places (the `CameraView` control on the page and `SymbologyMapper`), so replacing it is a small change.

**Tech Stack:** .NET MAUI 10, BarcodeScanning.Native.Maui 3.1.0 (MIT), CommunityToolkit.Mvvm 8.4.2.

**Spec:** `docs/superpowers/specs/2026-09-28-ibe-barcode-scanner-design.md`

## Global Constraints

(Same as prior plans.) `net10.0-android;net10.0-ios`; MIT; no database; `Nullable`/`ImplicitUsings` enabled.

---

### Task 1: Engine integration — DONE

**Files:** `src/IBEBarcode.Scanner/{MauiProgram.cs,Services/SymbologyMapper.cs}`

- [x] `UseBarcodeScanning()` registers the camera handler.
- [x] `SymbologyMapper` maps `BarcodeFormats`/`BarcodeTypes` onto the generator's `BarcodeSymbology`,
      including the EAN-13/UPC-A and Code 128/GS1-128 ambiguities (a Code 128 read with group separators or a
      plausible Application Identifier is reported as GS1-128).
- [x] `SymbologyMapper.ValueOf` prefers the engine's *raw* value, because only that preserves the GS1 group
      separators that mark the end of a variable-length element.
- [x] The requested format set lists what the generator prints plus the common retail extras, rather than
      `All`, so no engine wastes work on formats nobody here uses.

### Task 2: Camera page and lifecycle — DONE

**Files:** `src/IBEBarcode.Scanner/Views/ScanPage.xaml(.cs)`, `ViewModels/ScanViewModel.cs`

- [x] `CameraView` with aim mode, high capture quality, and the library's own vibration disabled (we do our
      own feedback so it can be turned off in settings).
- [x] Camera enabled on appearing and disabled on disappearing, so the app never holds the camera while
      another tab is showing.
- [x] Runtime camera permission with a clear explanation and a route into system settings if it is denied.
- [x] Torch toggle (persisted), camera switch, and a status line that doubles as the "starting camera" and
      error surface.

### Task 3: Continuous scanning, done properly — DONE

**Files:** `src/IBEBarcode.Scanner/{Services/ScanCoordinator.cs,ViewModels/ScanViewModel.cs}`,
`src/IBEBarcode.Scanner.Core/ScanDeduplicator.cs`

- [x] Detection bursts are collapsed: a repeat of the same value and symbology inside a three-second window
      produces no second result, no second haptic and no duplicate history entry.
- [x] The deduplication cache is bounded and prunes by age, so a long session cannot leak.
- [x] One result per detection burst, so the card is not overwritten several times a second.
- [x] Haptic confirmation on a real scan (vibration on Android, haptics on iOS), suppressible in settings.

### Task 4: Result actions — DONE

**Files:** `src/IBEBarcode.Scanner/Services/ScanResultActions.cs`, `ViewModels/ScanResultViewModel.cs`, `Views/ScanResultCard.xaml`

- [x] Raw payload → classification → symbology normalization → one `ScanResult`.
- [x] Open in the default browser through `Launcher`, only for the http/https link the classifier produced;
      a failure is reported rather than swallowed.
- [x] Copy to the clipboard (with confirmation), and share through the platform share sheet.
- [x] The raw value is shown only when it differs from the interpreted value, so identifiers and separators
      stay visible without cluttering every scan.

### Task 5: Scanning from a photo — DONE

**Files:** `ViewModels/ScanViewModel.cs`, `Services/FrameGrabber.cs`

- [x] Pick a photo with `MediaPicker`, run the native engine on it first, then the fallback decoders.
- [x] Failures are distinguishable: "could not open the photo" and "nothing could be decoded from that photo"
      are different messages.

### Task 6: On-demand scanning and the scan button (follow-up, after the first device test) — DONE

**Files:** `src/IBEBarcode.Scanner/{Views/ScanPage.xaml(.cs),ViewModels/ScanViewModel.cs}`

Prompted by the first real device test: the decoder ran continuously, which kept a 32-bit low-end phone busy,
and the user had to babysit the camera instead of aiming and pressing a button.

- [x] A large **Scan** button is the only way a scan starts, sitting in the middle of the button row and a
      head taller than the torch and camera buttons. `PauseScanning` is true whenever the user is not scanning;
      the preview keeps rendering, so the label can still be aimed.
- [x] One press scans; the same button (then showing **Stop**), a hit, or a 30-second timeout pauses again, and
      the deduplication window is reset per press so the label in view can be read again immediately.
- [x] The same press also runs the fallback decoders for MSI Plessey and Postnet, on a still frame sampled every
      five seconds, so the separate "Enhanced scan" button could go away. See Task 7.
- [x] Leaving the tab cancels the timer and releases the camera.

**Two facts about `BarcodeScanning.Native.Maui` 3.1.0 that are easy to get wrong** (read from the library
source, not assumed):

- Both platform analyzers return immediately while `PauseScanning` is true, and the shared
  `TriggerOnDetectionFinished` returns before raising `OnDetectionFinished` or `OnImageCaptured`. The analyzer —
  and therefore any frame capture — only runs while scanning is not paused.
- A still frame is attached only when `ForceFrameCapture` is true, or when `CaptureNextFrame` is true *and the
  engine detected a barcode in that frame*. Neither ML Kit nor Vision detects MSI Plessey or Postnet, so the
  enhanced scan must force the capture, or it can never be handed a frame.

The earlier enhanced scan paused the camera and then armed `CaptureNextFrame`, so no frame ever arrived: the
page stayed on "Capturing a frame…" with the button disabled for the rest of the session.

### Task 7: One Scan button for both decoders (follow-up) — DONE

**Files:** `src/IBEBarcode.Scanner/{Views/ScanPage.xaml(.cs),ViewModels/ScanViewModel.cs}`

- [x] The "Enhanced scan" button is gone; a live scan samples a forced still frame every five seconds and runs
      the MSI Plessey / Postnet decoders on it. A hit ends the scan exactly as a platform hit does. Sampling on
      an interval keeps the cost bounded: one frame measures ~1.9 s of CPU on the test phone.
- [x] The frame is collected through the camera control's `OnImageCaptured` **event**, not
      `OnImageCapturedCommand`. The library hands that command a bare `PlatformImage` while the event carries
      the typed `OnImageCapturedEventArg`, so a command typed for the event argument fails `CanExecute` and is
      silently skipped inside the library's own try/catch. That is why the earlier enhanced scan never once
      received a frame and only ever timed out; verified on the device afterwards (a 1600×900 frame every
      sample, no exception).
- [x] If sampling throws, it abandons only itself: the live platform scan continues.

### Task 8: One picture per press, no Stop button (follow-up) — DONE

**Files:** `src/IBEBarcode.Scanner/{Views/ScanPage.xaml,ViewModels/ScanViewModel.cs,Services/FrameGrabber.cs}`

Prompted by the second device test: with the start and stop of a scan on the same button, the user had to watch
that button, and a scan that found nothing went on looking busy for thirty seconds.

- [x] The Scan button is one action, not a toggle. `IsScanning`, `ScanDuration`, the 30-second timeout, the
      **Stop** state, the red button trigger and the live detection path (`OnDetectionFinishedCommand`) are
      gone; the button reads **Scan** always and is disabled only while a press is being read.
- [x] A press takes one picture: `PauseScanning` goes false for as long as it takes the control to hand over a
      forced frame, then straight back to true. The library delivers no frame while paused, which is why the
      engine is woken for that single frame instead of the capture being done out of band.
- [x] That picture is decoded twice: first `Methods.ScanFromImageAsync(byte[])` on a JPEG the new
      `FrameGrabber.EncodeJpeg` produces, then the MSI Plessey / Postnet decoders on the same pixels. The
      engine runs first so its answer is never delayed by the slower fallback pass.
- [x] A press that finds nothing shows `ScanNotFound` for three seconds and then reverts to the idle hint, so
      the app never looks busy after it has stopped working.

### Task 9: Every code in the picture, not the first (follow-up) — DONE

**Files:** `src/IBEBarcode.Scanner/{ViewModels/ScanViewModel.cs,ViewModels/ScanResultViewModel.cs,Views/ScanResultCard.xaml}`

Prompted by a real book label, which carries two barcodes in one shot, and by a press that reported only one of
them.

- [x] `DecodeAsync` keeps everything the engine read instead of taking `FirstOrDefault()`, and drops equal
      readings by symbology and value: `Methods.ScanFromImageAsync` scans the picture and its inverted copy, so
      the same label arrives twice.
- [x] The first code takes the result card; the rest are listed under it as "Also in the picture", each with its
      own value and its own Search Google / Open link / Copy links. "Scan a photo" shares the same code path, so
      a picture holding two codes behaves exactly like a camera shot holding two.
- [x] Every code found is registered in history, so the History tab shows both rather than one.
- [x] The fallback decoders still run only when the engine read nothing at all, so a picture it already
      understood does not also pay ~1.9 s of scanline decoding.

## Verification

```bash
dotnet build src/IBEBarcode.Scanner -f net10.0-android   # compiles, packages, no C# warnings
dotnet build src/IBEBarcode.Scanner -f net10.0-ios       # compiles (native iOS packaging needs a Mac)
```

Outstanding, and listed as such in the README and the device checklist: live-camera behaviour cannot be
verified without hardware. Emulator and real-device checks for every format the generator prints, for
low-light behaviour, for camera release when backgrounding, and for the UPC supplement caveat are all in
`docs/testing/device-checklist.md`.
