# IBE Barcode Scanner — Architecture Design

Date: 2026-09-28
Status: Approved

## Goal

Build **IBE Barcode Scanner**: a free, open-source (MIT) iOS and Android app that
reads printed barcodes with the camera and NFC tags where the hardware supports
it, shows what was read, and offers to open a link when the payload is one.
It must read the formats printed by the sibling project **IBE Barcode Generator**
(`../IBEBarcodeGeneratror`), so the two tools work as a pair.

## Requirements (from `requirements.txt`)

1. Reads standard printed barcodes, including QR codes, using the camera on iOS
   and Android phones. Reads NFC codes if the phone supports it.
2. Built with C# / .NET 10, cross-platform for iOS and Android.
3. Scanned values are displayed, and a scanned link can be opened in the default
   browser at the user's request.
4. No database dependency.
5. Localization: English, Simplified Chinese, Traditional Chinese, Japanese.
6. MIT license.

## Feature scope

- **Camera scanning** of every format IBEBarcodeGenerator can print that phone
  SDKs are able to read (see the coverage table below), on demand: the preview is
  live for aiming, and frames are decoded only while a scan is running. Torch,
  zoom-free aim assist, camera switching and duplicate suppression included.
- **The two formats no mobile scanning SDK supports** — MSI Plessey and USPS
  Postnet — are decoded from a still frame captured during an ordinary scan, by
  our own decoders. There is no separate mode or button: one press covers every
  format the app can read.
- **Photo scanning**: read a barcode from an image the user picks, using the
  native engine first and the fallback decoders after.
- **NFC**: read NDEF tags (link, text, email, phone, WiFi, vCard) on Android,
  including tags tapped while the app is closed.
- **Result intelligence**: classify the payload (link, email, phone, SMS, WiFi,
  contact, location, calendar, GS1, product code), parse GS1 Application
  Identifiers, normalize UPC-A/UPC-E/ISBN, and only ever hand http/https to the
  browser.
- **Session history** held in memory, with copy and share.
- **Four UI languages**, chosen from the system language with an in-app override.

## Format coverage decision (the central technical fact)

These two facts drove the architecture, and both were verified against the
generator's source and the SDK documentation:

1. **IBEBarcodeGenerator prints 20 formats**: Code 39, Extended Code 39, Code 93,
   Codabar, Interleaved 2 of 5, MSI Plessey, Code 128 (A/B/C), GS1-128, EAN-13,
   EAN-8, UPC-A, UPC-E, UPC 2-digit supplement, UPC 5-digit supplement, ISBN,
   Postnet, QR Code, Data Matrix, PDF417, Aztec.
2. **Google ML Kit (Android) and Apple Vision (iOS) cannot decode MSI Plessey or
   USPS Postnet — and neither can ZXing.Net or zxing-cpp.** There is no
   off-the-shelf mobile decoder for either format.

| Generator format | Android (ML Kit) | iOS (Vision) | This app |
|---|---|---|---|
| Code 39 / Extended Code 39, Code 93, Codabar, Interleaved 2 of 5 | yes | yes | native |
| Code 128, GS1-128 | yes | yes | native + our own GS1 AI parsing |
| EAN-13, EAN-8, UPC-A, UPC-E, ISBN | yes | yes | native + UPC-A/ISBN normalization |
| UPC 2/5-digit supplements | partial | partial | whatever the engine reports, plus a note |
| QR Code, Data Matrix, PDF417, Aztec | yes | yes | native |
| **MSI Plessey** | **no** | **no** | **fallback decoder on a captured frame** |
| **USPS Postnet** | **no** | **no** | **fallback decoder on a captured frame** |

## Decisions made during brainstorming

- **UI framework: .NET MAUI 10**, one project, `net10.0-android;net10.0-ios`.
  Chosen over separate `.NET for iOS` / `.NET for Android` projects because the
  app is one screen-sized tool, not two platform-specific products, and over
  Blazor Hybrid because the camera and NFC integrations are fundamentally native.
- **Scanning engine: `BarcodeScanning.Native.Maui` 3.1.0 (MIT, targets .NET 10)**,
  which wraps ML Kit on Android and Apple Vision on iOS. This deliberately parts
  with the generator's "no third-party barcode library" rule: encoding is
  deterministic and worth writing from scratch, whereas decoding camera frames is
  pattern recognition where a custom implementation would be far worse in exactly
  the conditions a phone app must handle. The engine sits behind our own
  `SymbologyMapper` + `ScanCoordinator` so it can be swapped.
- **Fallback decoding is our own code, not a library**, because no library
  decodes these formats. MSI Plessey is decoded from scanline run lengths using
  the generator's own bit encoding (wide bar = 1, wide space = 0); Postnet is
  decoded from bar *heights* (five bars per digit, exactly two tall, weights
  7-4-2-1-0, framed by guard bars, mod-10 check digit).
- **NFC is hand-written platform code**, not a plugin: Android
  `NfcAdapter.EnableReaderMode`, plus intent handling for tags that launch the
  app. Third-party MAUI NFC plugins are thinly maintained, and our requirement
  (read NDEF, show text or link) is about 200 lines.
- **iOS NFC is deferred to phase 2.** CoreNFC requires the
  `com.apple.developer.nfc.readersession.formats` entitlement, which requires a
  paid Apple Developer Program membership and a matching provisioning profile.
  `IosNfcReader` reports "not supported" and the UI says so; the abstraction,
  the NDEF parsing and the result path are already built and shared.
- **No database, and no persistence of scans.** History is an in-memory
  `ObservableCollection` for the session. Only settings (language, torch,
  feedback, raw-value display) are persisted, in `Preferences`, which is a
  key-value store rather than a database.
- **Scanning is one picture per press, and there is nothing to stop** (decided
  over two device tests). A low-end 32-bit test phone wedged while decoding frames
  continuously, and the user then had to watch the state of a Scan/Stop button
  instead of just aiming and pressing. A press of the big Scan button is now a
  snapshot: the analyzer is woken for exactly one frame, the picture is decoded,
  and the analyzer is put straight back to sleep. A press that finds nothing
  flashes a warning for a few seconds and returns to idle, so there is no scan to
  stop and no window in which the camera is sampled.
- **One button, two decoders.** The fallback decoders for MSI Plessey and Postnet
  began as a separate "Enhanced scan" button, which asked the user to know
  something about their own label that the app can work out for itself. The same
  picture the engine reads is handed to those decoders too, so one press reads
  anything the app can read. Decoding a frame costs about 1.9 seconds of CPU on
  the test phone — both formats are searched upright and rotated — which is
  affordable once per press and would not be per frame. The engine goes first, so
  a label it can read never waits for them.
- **Portrait only.** The preview, the Scan button and the result panel are laid out
  for a tall screen, so both platforms lock the orientation: Android on the
  activity, iOS in `Info.plist`.
- **Every code in the picture is reported.** One shot can hold several labels, and a
  book label usually holds two, so the engine's readings are all kept rather than the
  first one winning: the first takes the card, the rest are listed under it with their
  own value and their own links, and all of them are recorded. Equal readings are
  dropped, because the engine re-reads the same label from the inverted copy of the
  picture. The fallback decoders still run only when the engine read nothing at all, so
  a picture it already understood does not pay for a scanline pass.
- **A result offers its actions as links.** Search Google, Open link (only when the
  payload carried one), Copy and Share sit under the value in a wrapping row, in the
  colour and underline of something clickable. The scanned value is tappable as well,
  because it is the thing the user is looking at, and the hint under it says so.
- **Help lives in the app.** The instructions sit on the About tab, next to the
  version, the language picker and the format coverage, in all four languages.
  There is no external documentation to keep in step and nothing to fetch offline.
- **Localization: resx + `CultureInfo`**, with the strongly-typed accessor
  generated during the build (the documented .NET MAUI 10 CLI setup), four
  resource files, iOS `CFBundleLocalizations`, and per-locale Android app names
  and iOS permission strings. Changing language rebuilds the shell so every
  string, including Shell tab titles, refreshes at once.

## Solution structure

Single `.slnx` at the repository root, mirroring the generator repository.

```
/src
  IBEBarcode.Scanner        MAUI app (net10.0-android;net10.0-ios): views, view models,
                            platform services (camera, NFC, frame capture), 4-language resources
  IBEBarcode.Scanner.Core   net10.0, no MAUI dependency: symbology model, result model,
                            payload classification, GS1 AI parsing, UPC/EAN/ISBN normalization,
                            NDEF parsing, deduplication, MSI Plessey + Postnet decoders
/tests
  IBEBarcode.Scanner.Core.Tests
/docs/superpowers/specs     this design
/docs/superpowers/plans     one plan per subsystem, dated
/docs/testing               manual device checklist
```

## Core data model

- `BarcodeSymbology` — mirrors `IBEBarcode.Core.BarcodeSymbology` so a scanned
  label is named the same way the generator named it.
- `ScanResult` — symbology, kind, raw text, display text, source, timestamp, link,
  GS1 elements, ISBN-13/10, expanded UPC-A.
- `PayloadClassifier` — decides what a payload *is*, and is the only place that
  decides whether something may be opened in a browser (http/https only; `tel:`,
  `mailto:`, `sms:`, `geo:`, `WIFI:` keep their own kind and are never launched).
- `Gs1AiParser` — compact (FNC1/GS-separated) and bracketed `(01)…` notations,
  with an AI table; unknown AIs keep their data instead of being dropped.
- `UpcEanNormalizer` — EAN-13→UPC-A when the leading zero is present, Bookland
  978/979→ISBN (including ISBN-10), UPC-E→UPC-A expansion.
- `NdefPayloadReader` — NDEF message parsing: URI records with well-known prefix
  abbreviation, text records (UTF-8/UTF-16 + language), MIME and external types.
- `GrayImage` + `MsiPlesseyDecoder` + `PostnetDecoder` + `BarcodeFrameDecoder` —
  the fallback chain, working on grayscale pixels so it is testable without a
  device.
- `ScanDeduplicator` — suppresses repeats of the same value within a window, so
  one label held in the camera during a single scan reports once. Pressing Scan
  resets it, so the label in view can be read again immediately.

## Out of scope for this design

- Reading the formats neither the generator prints nor this app claims
  (MaxiCode, rMQR, GS1 DataBar beyond what the engines support natively).
- Batch/inventory workflows, accounts, cloud sync, analytics, any database.
- Windows/Mac Catalyst targets: the requirements say phones and pads.
- Writing NFC tags: the generator's output is read-only as far as the scanner is
  concerned.
