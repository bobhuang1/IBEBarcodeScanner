# IBE Barcode Scanner

Free, open-source (GPL-3.0) iOS and Android app that scans printed barcodes, QR codes
and NFC tags — the companion of
[IBE Barcode Generator](../IBEBarcodeGeneratror). It reads every format the
generator can print that phone hardware is capable of reading, and holds the
line on two formats no mobile scanning SDK supports by decoding them itself.

Built with C# / .NET 10 and .NET MAUI. No database, no account, no telemetry:
scans live in memory for the session and nothing leaves the device.

## Status

Working app, ready for device testing:

- Camera scanning of Code 39 / Extended Code 39, Code 93, Code 128, GS1-128,
  Codabar, Interleaved 2 of 5, EAN-13, EAN-8, UPC-A, UPC-E, ISBN, QR Code,
  Data Matrix, PDF417 and Aztec. Scanning is one picture per press: the preview
  stays live for aiming, the big **Scan** button takes a single frame and tries to
  read it, and nothing is captured or decoded in between. A press that finds
  nothing says so for a few seconds and then goes quiet. Torch and camera switch
  are there too, and the app is locked to portrait.
- **One Scan button for everything.** That one picture goes to the platform engine
  first and to our own decoders for MSI Plessey and USPS Postnet second, so the
  user never picks a mode or needs to know which label they are holding. Scanning
  from a photo works the same way.
- **Every code in the picture, not just the first.** One press can hold several
  labels, and a book label usually carries two codes; the first takes the result
  card and the rest are listed underneath it as "Also in the picture", each with its
  own value and its own links, and each recorded in history.
- **Results do something.** Every result offers links: **Search Google** looks the
  value up in the default browser (tapping the value itself does the same), **Copy**
  puts it on the clipboard, and **Share** passes it on. A link found inside the payload
  is shown under the value as its own link, with **Open link** beside the others.
- Payload interpretation: links (opened in the default browser only on request),
  email, phone, SMS, WiFi, contacts, calendar, GS1 element strings (with
  Application Identifier parsing) and product codes (UPC-A/UPC-E/ISBN
  normalization).
- NFC tag reading on Android, including tags tapped while the app is closed.
- Session history with copy and share.
- English, Simplified Chinese, Traditional Chinese and Japanese UI.

Known limitations are listed below, and the manual device checks that are still
outstanding are tracked in `docs/testing/device-checklist.md`.

## Solution layout

- `src/IBEBarcode.Scanner` — .NET MAUI app (`net10.0-android`, `net10.0-ios`).
  Views, view models, the camera/NFC/frame services and the four-language UI.
  The launcher icon and splash are generated from one file, the IBE Group web
  site's `images/logosmall.jpg`, which is also the logo on the About tab (copied in
  as `Resources/Images/ibegroup_logo.jpg`); the generated icon layers live in
  `Resources/AppIcon`.
- `src/IBEBarcode.Scanner.Core` — `net10.0`, no UI or platform dependency:
  symbology and result models, payload classification, GS1 AI parsing,
  UPC/EAN/ISBN normalization, NDEF parsing, deduplication, and the MSI Plessey
  and USPS Postnet decoders. This is where the testable logic lives.
- `tests/IBEBarcode.Scanner.Core.Tests` — xUnit tests, including synthetic
  images rendered with the exact patterns IBEBarcodeGenerator produces.
- `docs/superpowers/specs` — architecture design.
- `docs/superpowers/plans` — one implementation plan per subsystem.
- `docs/testing/device-checklist.md` — the manual test matrix for real hardware.
- `docs/testing/packaging-and-ci.md` — the release artifacts, the signing material and the CI/CD
  pipelines, including what an iPhone build actually needs.
- `CLAUDE.md` — the internal handoff document: standing architectural decisions that are not up for
  renegotiation, the environment and its traps, commit rules, and where the project was left off.
  Read it before changing anything in this repository.

## Build and test

```bash
dotnet build                                # everything (Android + iOS + tests)
dotnet test                                 # core logic tests, no device needed
dotnet build src/IBEBarcode.Scanner -f net10.0-android
dotnet build src/IBEBarcode.Scanner -f net10.0-ios
```

Building the iOS target needs a Mac with Xcode (or a GitHub Actions macOS
runner); the C# side compiles anywhere. Running on a device:

```bash
dotnet build src/IBEBarcode.Scanner -t:Run -f net10.0-android
```

See `docs/testing/android-deploy.md` for the Visual Studio 2026 walkthrough and for fixing a phone that
`adb devices` cannot see.

## Releases

Every push is built by [build.yml](.github/workflows/build.yml): the core tests run on Linux, a
Release APK is published as a downloadable artifact, and the iOS target is compiled on macOS to
prove it still builds. Pushing a tag runs [release.yml](.github/workflows/release.yml), which
attaches installable files to a GitHub Release:

| File | For |
|---|---|
| `…-arm64-v8a.apk` | 64-bit ARM phones (26 MB) |
| `…-armeabi-v7a.apk` | 32-bit ARM phones (26 MB) |
| `…-x86_64.apk` | emulators and some Chromebooks |
| `…-universal.apk` | any phone, if you would rather not choose (53 MB) |
| `….aab` | Google Play upload |
| `….ipa` | iPhone, when Apple signing material is configured |

```bash
git tag v1.0.1 && git push origin v1.0.1
```

Bump `<ApplicationDisplayVersion>` in the project file before tagging. Releases are signed from
repository secrets when they are configured (see `docs/testing/packaging-and-ci.md` for the
keystore and the Apple side); without them the APKs are debug-signed and say so, which is fine for
testing but cannot update an earlier install. A signed iPhone `.ipa` needs an Apple account — a
free one is enough for a 7-day development build on registered devices, and iOS NFC needs the paid
program.

## Format support

| Format | Android | iOS | How |
|---|---|---|---|
| Code 39, Code 39 Extended, Code 93, Codabar, Interleaved 2 of 5 | yes | yes | ML Kit / Apple Vision |
| Code 128, GS1-128 | yes | yes | native engine + our GS1 Application Identifier parsing |
| EAN-13, EAN-8, UPC-A, UPC-E, ISBN | yes | yes | native engine + our UPC-A/UPC-E/ISBN normalization |
| UPC 2-digit / 5-digit supplements | engine-dependent | engine-dependent | shown when the engine reports them |
| QR Code, Data Matrix, PDF417, Aztec | yes | yes | native engine |
| **MSI Plessey** | from a scanned frame | from a scanned frame | our scanline decoder (no SDK supports it) |
| **USPS Postnet** | from a scanned frame | from a scanned frame | our bar-height decoder (no SDK supports it) |

MSI Plessey and Postnet are decoded from the same picture an ordinary **Scan**
press takes rather than from every preview frame, so they need the label to be
flat, close and well lit, and a press takes a little longer when the engine finds
nothing and they get their turn. The decoder for
MSI follows the bit encoding used by `IBEBarcode.Core.Encoders.MsiPlesseyEncoder`
(wide bar = 1, wide space = 0, guard patterns at both ends, optional mod-10 check
digit); the Postnet decoder follows `PostnetEncoder` and `HeightBarRenderer`
(bottom-aligned bars, five bars per digit, exactly two tall, mod-10 check digit).

## NFC

Android reads NDEF tags through `NfcAdapter` reader mode, and also handles tags
that launch the app through an NDEF/TECH intent. A tag containing a URI produces
the same result card as a camera scan, with the same tappable link row and
open-in-browser behaviour; text, email, phone and WiFi payloads are classified
and displayed.

iOS needs the CoreNFC entitlement
(`com.apple.developer.nfc.readersession.formats`), which requires a paid Apple
Developer Program membership. Until that exists, `IosNfcReader` reports NFC as
unsupported and the UI explains it — camera scanning is unaffected. The interface,
the NDEF parsing and the result path are shared, so enabling it is a change to one
file plus the entitlement.

## Known limitations

- **iOS NFC** is not available in this version (entitlement, see above).
- **MSI Plessey and USPS Postnet** need a steady, close, well-lit label. They are
  read from the picture a press takes rather than continuously, so a press that
  finds nothing takes a moment longer while they search the frame.
- **Android scanning uses Google ML Kit**, which needs Google Play services. On a
  GMS-free device the camera path is unavailable (the captured-frame decoders and
  NFC still work); the app should surface that rather than failing silently.
- **UPC 2/5-digit supplements** are only shown if the underlying engine reports
  them, which varies by platform and OS version.
- **Linked libraries**: `BarcodeScanning.Native.Maui` (MIT). ML Kit and Apple
  Vision are platform frameworks used royalty-free.

## License

This project is free software, released under the **GNU General Public License v3.0**. You may redistribute and/or modify it under those terms; see [LICENSE.md](LICENSE.md) for the full text.
