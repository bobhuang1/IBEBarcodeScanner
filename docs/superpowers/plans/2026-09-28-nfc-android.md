# NFC Plan (Android in v1, iOS behind the same interface)

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Read an NFC tag, display what it contains, and offer to open it when it contains a link. Works on
Android now; iOS is wired but reports "not supported" until the Apple entitlement exists.

**Architecture:** One contract (`INfcReader`) with two implementations chosen at registration time; a shared
`NfcService` that owns the state machine and turns NDEF bytes into a `ScanResult` through the same
`ScanCoordinator`, `ScanResultFactory` and result card as a camera scan. NDEF parsing itself lives in Core and
is unit-tested against hand-built message bytes.

**Tech stack:** Android `NfcAdapter` reader mode (no third-party NFC plugin), Core NFC deferred, .NET 10, xUnit.

**Spec:** `docs/superpowers/specs/2026-09-28-ibe-barcode-scanner-design.md`

## Platform reality this plan is built around

- **Android** reads tags while the app is open (reader mode, foreground), and can also be launched by a tag
  through an NDEF/TECH intent. Reader mode is used instead of the older foreground-dispatch NDEF push because
  it also handles tags without NDEF, and it disables peer-to-peer beaming while active. `NFC` permission and
  `<uses-feature android:name="android.hardware.nfc" android:required="false"/>` are both declared, so the app
  still installs on devices without NFC.
- **iOS** cannot read tags at all without the `com.apple.developer.nfc.readersession.formats` entitlement,
  which requires a paid Apple Developer Program membership and a matching provisioning profile. The plan was
  approved with iOS NFC moved to a later phase for that reason.

---

### Task 1: NDEF parsing in Core — DONE

**Files:** `src/IBEBarcode.Scanner.Core/NdefPayloadReader.cs`

- [x] Parse an NDEF message: single or four-byte payload lengths, ID lengths, short/long records, and chunked
      records reassembled into one record.
- [x] Well-known URI records with the full NFC Forum prefix-abbreviation table, so `0x02 + "example.com"`
      becomes `https://www.example.com`.
- [x] Text records: UTF-8 and UTF-16, language code, status byte.
- [x] MIME records (`text/…`, JSON, XML) and external/unknown records surface their UTF-8 text rather than
      hiding the tag's content.
- [x] Only http/https produce a `Uri`; `tel:`, `mailto:`, `urn:` and custom schemes keep their text form and
      are never handed to the platform launcher.
- [x] Malformed or truncated bytes return null instead of throwing.

### Task 2: Reader contract and service — DONE

**Files:** `src/IBEBarcode.Scanner/Services/Nfc/{INfcReader.cs,NfcService.cs}`

- [x] `INfcReader` exposes support/enabled/reading state, a start/stop pair, and a `TagRead` event carrying
      either NDEF bytes or a status (`Empty`, `Unreadable`, `Unsupported`, `Disabled`).
- [x] `NfcService` maps statuses to a state machine the UI can render, parses the NDEF message, builds a
      `ScanResult` through `ScanResultFactory`, and records it in the session history — so a tag read counts
      exactly like a camera scan.
- [x] A tag read while the app was closed still lands in history even though no page was alive to see it.

### Task 3: Android implementation — DONE

**Files:** `src/IBEBarcode.Scanner/Platforms/Android/{AndroidNfcReader.cs,MainActivity.cs,AndroidManifest.xml}`

- [x] Reader mode with the NFC-A/B/F/V and barcode flags, using the current activity from MAUI's platform API.
- [x] NDEF read on a background callback (`Ndef.Connect` → `NdefMessage.ToByteArray` → `Close`), with the
      unreadable/empty outcomes reported rather than thrown.
- [x] Reader mode stopped on pause/stop, so the app does not keep the NFC service busy in the background.
- [x] `MainActivity` handles NDEF/TAG/TECH intents (`LaunchMode.SingleTop` for tags tapped while running),
      consumes the intent action afterwards so rotations and resumes do not replay the same tag, and forwards
      the message into the shared service through `IPlatformApplication.Current.Services`.
- [x] Permissions and feature declarations, with NFC optional.

### Task 4: iOS placeholder — DONE

**Files:** `src/IBEBarcode.Scanner/Platforms/iOS/IosNfcReader.cs`

- [x] Implements the contract honestly: not supported, no fake reads.
- [x] Documents exactly where CoreNFC goes (an `NFCNDEFReaderSession` delegate raising `TagRead`), so the
      phase-2 change is confined to this file plus the entitlement.
- [x] `Info.plist` already carries `NFCReaderUsageDescription`, localized into all four languages, so enabling
      it does not require touching the plist.

### Task 5: NFC page — DONE

**Files:** `src/IBEBarcode.Scanner/Views/NfcPage.xaml(.cs)`, `ViewModels/NfcViewModel.cs`

- [x] Start/stop button, status text that explains *why* nothing is happening (unsupported on iOS, NFC turned
      off, unreadable tag, empty tag), and the shared result card.
- [x] Status messages are localized, and the iOS message is specific rather than a generic failure.

## Verification

```bash
dotnet test tests/IBEBarcode.Scanner.Core.Tests --filter "FullyQualifiedName~Ndef"
dotnet build src/IBEBarcode.Scanner -f net10.0-android
```

Covers NDEF parsing for URI (abbreviated and full), text (UTF-8 and UTF-16), chunked records, unknown record
types, tel: URIs (which must not become links), and garbage bytes.

**Outstanding:** no physical tag has been tapped. `docs/testing/device-checklist.md` lists the Android checks:
a URL tag, a text tag, an empty tag, NFC switched off, a tag tapped while the app is closed, and a device
without NFC hardware. This is the honest boundary of what a Windows/Linux development machine can prove.
