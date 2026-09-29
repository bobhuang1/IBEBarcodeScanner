# Fallback Decoders Plan: MSI Plessey and USPS Postnet

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Read the two formats IBEBarcodeGenerator prints that no mobile scanning SDK can decode. Without
this, the scanner cannot fully satisfy "it should be able to scan barcodes created by IBEBarcodeGenerator".

**Architecture:** A platform-neutral grayscale pipeline in `IBEBarcode.Scanner.Core`: a `GrayImage`, an
Otsu-capable `Binarizer`, run-length extraction, two format-specific decoders and a chain
(`BarcodeFrameDecoder`) that runs them, upright and rotated a quarter turn. The app contributes only pixel
extraction, in `Platforms/{Android,iOS}/FrameGrabber.*.cs`; the decoders themselves never see a platform type.

**Scope decision (deliberate):** the first draft of this plan reached for ZXing.Net's MSI reader, with a
custom decoder only for Postnet. That was dropped: ZXing.Net cannot read Postnet at all, brings an
Apache-2.0 dependency into an MIT app, and its MSI reader is documented by its own authors as producing too
many false positives. Writing both decoders from the generator's own encoding rules keeps the dependency
tree MIT-only, makes the exact target patterns the only thing we optimise for, and — the deciding factor —
makes the whole pipeline testable with synthetic images on any OS, with no device and no camera.

**Tech Stack:** .NET 10, xUnit. No image libraries: the frames arrive as `byte[]` gray or BGRA/ARGB pixels.

**Spec:** `docs/superpowers/specs/2026-09-28-ibe-barcode-scanner-design.md`

## Verified reference values used by this plan's tests

The patterns are not guessed; they come from the generator's source and tests:

- **MSI Plessey** (`IBEBarcode.Core.Encoders.MsiPlesseyEncoder`): start = wide bar + narrow space; each digit
  = four BCD bits, most significant first, where `1` = wide bar *and* narrow space and `0` = narrow bar *and*
  wide space; stop = narrow bar, wide space, narrow bar. Digit `0` therefore produces segments
  `1,2,1,2,1,2,1,2` and digit `9` produces `2,1,1,2,1,2,2,1` (both asserted by the generator's own tests and
  reused here). Check digit: from the right, double every other digit, subtract 9 when over 9, complement the
  sum mod 10 — `ComputeCheckDigit("1234567") == '4'`, the generator's own reference value.
- **USPS Postnet** (`IBEBarcode.Core.Encoders.PostnetEncoder`, `HeightBarRenderer`): bars are equal width,
  evenly spaced and bottom-aligned; each digit is five bars with exactly two tall (weights 7-4-2-1-0);
  a full-height guard bar frames each end; the last five bars are a mod-10 check digit over the payload
  digits. Renderer defaults (used by the tests) are 2px bars, 2px gaps, 30px tall, 15px short.

---

### Task 1: Imaging primitives — DONE

**Files:** `src/IBEBarcode.Scanner.Core/Imaging/{GrayImage.cs,Binarizer.cs}`, `Decoding/RunLengthScanner.cs`

- [x] `GrayImage` — width, height, row-major 8-bit pixels, indexer, row access, BT.601 luminance from BGRA
      (Android) and ARGB (iOS) buffers, transpose, nearest-neighbour downscale.
- [x] `Binarizer` — Otsu threshold over a histogram, for both pixel buffers and integer bar heights.
- [x] `RunLengthScanner` — alternating ink/background runs from a scanline, skipping the leading quiet zone
      and dropping the trailing one so the run list ends on an ink run, which is what the 1D decoders expect.

### Task 2: MSI Plessey decoder — DONE

**Files:** `src/IBEBarcode.Scanner.Core/Decoding/MsiPlesseyDecoder.cs`

- [x] Classify each run as one or two modules relative to the narrowest run, rejecting widths that sit between
      the two levels rather than guessing which they are.
- [x] Parse the guard patterns and the bit pairs, rejecting the parse if the run sequence ends anywhere other
      than the stop pattern — which is what stops human-readable text, a neighbouring barcode or a cropped
      symbol from being reported as digits.
- [x] Reject any four-bit group above 9 (not a valid digit).
- [x] Report the decoded digits, whether the last digit is a valid mod-10 check digit, and the value without
      it; the generator does not append a check digit automatically, so a label without one must still read.
- [x] Allow the scanline to start a few ink runs in, so a speck before the start bar does not defeat the read.

### Task 3: USPS Postnet decoder — DONE

**Files:** `src/IBEBarcode.Scanner.Core/Decoding/PostnetDecoder.cs`

- [x] Determine the symbol band from the tallest bars, which excludes printed text below the symbol from the
      height measurement entirely.
- [x] Measure ink height per column inside the band, extract bars with a low threshold, then split tall from
      short by clustering the observed heights (seeded with the extremes, refined iteratively) instead of
      relying on a global threshold that could land on top of the short bars.
- [x] Validate the structure: guard bars full height, exactly two tall bars per digit, bar count matching
      5, 9 or 11 payload digits, and the mod-10 check digit. A digit that does not match the two-of-five table
      aborts the read, which is what makes a misread bar height a rejection rather than a wrong ZIP code.

### Task 4: Frame decoder chain — DONE

**Files:** `src/IBEBarcode.Scanner.Core/Decoding/BarcodeFrameDecoder.cs`

- [x] Downscale to a 1600px working width, then run Postnet (whole frame) and MSI (sampled scanlines across
      the upper three quarters of the frame, where the bars are, above any human-readable text).
- [x] Retry a quarter turn rotated when the upright pass found nothing.
- [x] Require Postnet reads to pass their check digit; for MSI, vote across scanlines and report the check-digit
      status so the UI can warn when it could not be verified.
- [x] Offer a "verified only" mode for callers that would rather show nothing than an unverified MSI value.

### Task 5: Platform pixel extraction — DONE

**Files:** `src/IBEBarcode.Scanner/Services/FrameGrabber.cs`, `Platforms/Android/FrameGrabber.Android.cs`, `Platforms/iOS/FrameGrabber.iOS.cs`

- [x] Shared partial class with the two operations the app needs: convert a captured camera frame, and decode
      a photo file.
- [x] Android: `Bitmap.CreateScaledBitmap` + `GetPixels` → ARGB → luminance; photos through
      `BitmapFactory.DecodeStreamAsync`.
- [x] iOS: redraw into an 8-bit device-gray `CGBitmapContext` via unmanaged memory (which also performs the
      downscale), then copy out; photos through `UIImage.LoadFromData`.
- [x] Both implementations return null rather than throwing, so a frame that cannot be converted degrades to a
      message instead of a crash.

### Task 6: App wiring — DONE

**Files:** `src/IBEBarcode.Scanner/ViewModels/ScanViewModel.cs`, `Views/ScanPage.xaml(.cs)`

- [x] The fallback decoders run behind the ordinary **Scan** button: a live scan samples a forced still frame
      every few seconds, decodes it off the UI thread, and ends the scan on a hit exactly as a platform hit
      does. (This replaced a separate "Enhanced scan" button, which also never worked — see Task 6 of
      `2026-09-28-camera-scanning.md`.)
- [x] "Scan a photo" runs the native engine first (`Methods.ScanFromImageAsync`) and only then the fallback
      decoders, so clean flat images use the better engine.
- [x] Unverified check digits are surfaced in the result card instead of being presented as fact.

### Task 7: One picture per press (follow-up) — DONE

- [x] These decoders now run on the single frame a Scan press captures rather than on a still frame sampled
      every few seconds, and the picture is shared: the platform engine reads it first
      (`Methods.ScanFromImageAsync`) and the decoders get the same pixels afterwards. One capture per press, no
      sampling loop. See Task 8 of `2026-09-28-camera-scanning.md`.

## Verification

```bash
dotnet test tests/IBEBarcode.Scanner.Core.Tests --filter "FullyQualifiedName~MsiPlessey|FullyQualifiedName~Postnet|FullyQualifiedName~FrameDecoder|FullyQualifiedName~GrayImage"
```

Covers: every MSI digit count including check-digit and no-check-digit labels, several module widths, damaged
and truncated symbols, uniform-width (non-MSI) input, a leading speck, Postnet at 5, 9 and 11 digits, flipped
check digits, missing guard bars, illegal digit counts, narrow-bar/tight-spacing renders, text printed below
the symbol, quarter-turn rotation, and random noise producing no results. 44 tests in total for this plan.

**Still outstanding, and deliberately not claimed:** no test here has seen a real camera frame printed by
IBEBarcodeGenerator. `docs/testing/device-checklist.md` carries the device checks, and PNG fixtures exported
from the generator app would strengthen the suite — the synthetic renders already match the generator's
geometry exactly, so that is a hardening step rather than a gap in coverage.
