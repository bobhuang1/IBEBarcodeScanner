# Device Test Checklist

Everything in `IBEBarcode.Scanner.Core` is covered by automated tests (`dotnet test`, 128 passing). This
document covers what a test cannot: cameras, tags, permissions, launchers and platform quirks.

Getting the app onto a phone first: see `android-deploy.md`.

**Test material:** a printed sheet exported from IBE Barcode Generator containing, for each format, a label
with a known value:

| # | Format | Sample value |
|---|---|---|
| 1 | Code 39 | `IBE-1234` |
| 2 | Extended Code 39 | `ibe-1234/ab` |
| 3 | Code 93 | `IBE93-42` |
| 4 | Code 128 Set B | `IBE-128-B` |
| 5 | GS1-128 | `(01)09501101530003(17)251231(10)LOT42` |
| 6 | Codabar | `A123456B` |
| 7 | Interleaved 2 of 5 | `1234567890` (even digit count) |
| 8 | EAN-13 | `5901234123457` |
| 9 | EAN-8 | `96385074` |
| 10 | UPC-A | `036000291452` |
| 11 | UPC-E | `04252614` |
| 12 | UPC 2-digit supplement | `036000291452 12` |
| 13 | UPC 5-digit supplement | `036000291452 51234` |
| 14 | ISBN | `9780306406157` |
| 15 | MSI Plessey | `1234567` (no check digit) and `12345674` (with check digit) |
| 16 | MSI Plessey | `90210` |
| 17 | Postnet | `12345`, `90210`, `123456789`, `12345678901` |
| 18 | QR Code | `https://ibebarcode.com` |
| 19 | Data Matrix | `IBE-DM-0001` |
| 20 | PDF417 | `IBE-PDF417-0001` |
| 21 | Aztec | `IBE-AZTEC-0001` |

## Automated (already green)

- [x] `dotnet build IBEBarcodeScanner.slnx` — Core, tests and the app for `net10.0-android` and `net10.0-ios`.
- [x] `dotnet test` — 145 tests: payload classification, GS1 AI parsing, UPC/EAN/ISBN normalization, NDEF
      parsing, deduplication, imaging primitives, MSI Plessey decoding, Postnet decoding, the frame decoder
      chain, the Google-search URL builder, and the four localization files.
- [x] Decoder tests render the generator's exact MSI Plessey and Postnet patterns synthetically, including
      damaged, truncated, rotated, text-contaminated and noisy inputs.

## Android — requires a device (or emulator with a virtual scene camera)

> Run on the **Stratus_C7** test phone (480×960, 32-bit Helio A22) on 2026-09-28. Checks that need only the
> phone — the permission flow, the scan button, tab switching, the still-frame capture — are ticked; the ones
> that need the printed test sheet are not.

### Camera scanning

- [x] 1. First launch asks for camera permission with a clear explanation; denying it shows the settings route
      instead of a blank screen. Granting it starts the preview.
      *(2026-09-28: the system dialog appears, granting starts the preview, and denying shows the "Camera
      access needed" alert with **Open settings** and **OK** — no blank screen.)*
- [ ] 2. Each of labels 1–14 and 18–21 scans from the live camera after pressing **Scan**, and the card shows
      the right value and the right symbology name.
- [ ] 3. Label 10 (UPC-A) is reported as **UPC-A** (`036000291452`), not as EAN-13 with a leading zero.
- [x] 4. Label 14 (ISBN) is reported as **ISBN**, showing ISBN-13 and ISBN-10. *(2026-09-28: a real book
      cover came back as ISBN-13 `9780345514400` / ISBN-10 `0345514408` on the first press. The printed label 14
      is still worth running for the generator's own check digits.)*
- [ ] 5. Label 11 (UPC-E) shows the UPC-E value and the expanded UPC-A (`042100005264`).
- [ ] 6. Label 5 (GS1-128) shows bracketed elements `(01)…(17)…(10)…` and lists each AI with its name.
- [ ] 7. Labels 12–13: record what actually happens (the engines differ) and whether the supplement is shown,
      attached or ignored. This is the one format pair the plan does not promise.
- [ ] 8. One label held in front of the camera during a single **Scan** press vibrates/beeps once, and only
      once per press that finds it.
- [ ] 9. Torch toggle works and its state survives an app restart.
- [ ] 10. Camera switch works and the preview recovers.
- [ ] 11. Aim mode's indicator appears and the scan still works in low light.
- [x] 12. Switching to another tab stops any running scan and releases the camera (no "camera in use"
      conflict when returning). *(2026-09-28.)*
- [ ] 13. Backgrounding the app releases the camera; returning brings back the live preview, still paused until
      **Scan** is pressed.
- [x] 14. QR code with `https://ibebarcode.com` shows the value with "Tap to search the web" and a **Link**
      row underneath holding the URL; tapping the link opens the default browser. *(2026-09-28: the value
      opened a Google search in Chrome, and the link row opened the URL itself.)*
- [x] 60. One press reports every code in the shot, not just the first: a synthetic label carrying an ISBN and
      a price code side by side (`two-codes.png`) came back as two results — ISBN `9780345514400` in the card and
      UPC-A `076714005990` under **Also in the picture**, each with its own Search Google / Copy links — and the
      History tab then showed both as separate scans. *(2026-09-28, through "Scan a photo". The fixture is
      rendered by `scratch/BarcodeFixtures`; a copy sits in `/sdcard/Pictures/bcfixtures` on the phone. Still to
      see: a real two-code label through the camera, since the book used for testing has been put away.)*
- [x] 59. The result card's action links do what they say: **Search Google** hands the value to the default
      browser (verified with ISBN `9780345514400`, which opened the browser with the value in its search box),
      **Copy** puts the value on the clipboard and confirms it with "Copied to the clipboard.", and **Open
      link** appears only for a payload that carries a link. *(2026-09-28.)*
- [x] 57. Tapping the value of a plain-text payload searches it on the web, and no link row appears.
      *(2026-09-28.)*
- [ ] 15. A QR code containing plain text shows **no** link row and no **Open link** action.
- [ ] 16. A QR code containing `javascript:alert(1)` (build one if needed) shows text only, and never opens
      anything.

### Scan button (manual scanning)

Scanning is never automatic, and there is no scan to stop: the preview is live so the label can be aimed, and a
press of **Scan** takes one picture and reads it. Numbered 44–48 so the checks above keep their numbers.

- [x] 44. Opening the Scan tab shows "Tap Scan to read a barcode." and a large **Scan** button, with a live
      preview. *(2026-09-28.)*
- [x] 45. Pressing **Scan** takes one picture and reads the label in view: the status shows "Reading the
      barcode…", the result card fills in, and the button is unavailable only for that moment.
      *(2026-09-28: three consecutive presses on a real paperback's cover barcode each read it on the first try,
      about 1.5 s after the tap. The printed sheet is still worth running for the other formats.)*
- [x] 58. A real book, end to end: the cover's UPC-A reads as **UPC-A** `076714005990` (the engines' leading-zero
      EAN-13 `0076714005990`, undone by `UpcEanNormalizer`) with type **Product code**, source **Camera**.
      Worth knowing: book catalogues often record that same number in their ISBN field, so a book barcode is
      not automatically an ISBN — the 978/979 Bookland symbol is the one that must come back as **ISBN** with
      ISBN-13 and ISBN-10, and that is label 14. *(2026-09-28.)*
- [x] 46. Nothing is decoded between presses: holding a label in front of the camera after a press produces no
      further result by itself. *(2026-09-28.)*
- [x] 47. *Superseded.* This check used to see a scan pause itself after 30 seconds with "Camera paused";
      there is no scan window and no **Stop** any more. See 45 and 54.
- [ ] 48. A 10-minute session of repeated scans neither wedges the phone nor makes it hot (the failure that
      motivated the button).
- [x] 49. Every button is visible and tappable on a 360 dp-wide phone: **Scan** in the middle of the row and a
      head taller than the torch and camera buttons, with "Scan a photo" on its own row. *(2026-09-28: the old
      row of four pushed "Scan a photo" off the right edge, where it could not be tapped.)*
- [x] 50. A press that finds nothing ends cleanly: no capture stays armed, no timer keeps running, and the
      next press behaves the same. *(2026-09-28.)*
- [x] 53. The still-frame capture really delivers: consecutive presses each captured a frame and decoded it
      (measured at ~1.9 s per frame on this phone) with no exception. *(2026-09-28.)*
- [x] 54. A press that finds nothing shows "No barcode found. Hold the label steady and try again." for about
      three seconds and then reverts to "Tap Scan to read a barcode.", leaving no result and no warning behind.
      *(2026-09-28: verified by pressing with the front camera pointed at the room.)*
- [x] 55. The app is locked to portrait: with auto-rotate off and the display forced to landscape
      (`settings put system user_rotation 1`), the window stays `port` with `mRotation=ROTATION_0`.
      *(2026-09-28.)*
- [x] 56. The About tab explains how to use the app — "How to use": scanning, photos, results, NFC and tips —
      in the selected language, above the format coverage. *(2026-09-28.)*

### MSI Plessey and Postnet (decoded from a frame captured during the same scan)

These used to be a separate "Enhanced scan" button. They are now part of the ordinary **Scan** press, so a
plain scan should find them on its own.

- [ ] 17. Label 15 (`1234567`): pressing **Scan** while holding the label decodes it; the card warns that the
      check digit was not verified.
- [ ] 18. Label 16 (`12345674`): decodes as `12345674` with no warning.
- [ ] 19. Labels 17 (`12345`, `90210`, `123456789`, `12345678901`): decoded from a plain **Scan**.
- [ ] 20. Hold a Postnet label at an angle and in dim light: it should be refused, not misread.
- [ ] 21. "Scan a photo" works for a photo of labels 15–17 and for a photo of a QR code.

### NFC

- [ ] 22. A device without NFC shows "no NFC hardware" rather than an error.
- [ ] 23. With NFC switched off in system settings, the page says so.
- [ ] 24. Start an NFC scan and tap a tag containing a URL: the result appears, with open-in-browser.
- [ ] 25. Tap a tag containing plain text: shown as text, no link button.
- [ ] 26. Tap a tag with `tel:` content: classified as a phone number and **not** offered as a link.
- [ ] 27. Tap an empty/NDEF-less tag: a clear message, no crash.
- [ ] 28. With the app closed, tap a tag: the app opens and shows the tag's content.
- [ ] 29. NFC reads appear in the history with source "NFC tag".
- [ ] 30. Leaving the NFC tab stops reader mode (no notification sound while elsewhere in the app).

### Language and layout

- [ ] 31. With the system language set to 简体中文, 繁體中文 and 日本語, a fresh install starts in that language.
- [ ] 32. The Android launcher label is translated in each of those languages.
- [ ] 33. Changing language in About rebuilds the UI, including tab titles, without restarting the app.
- [ ] 34. Long values (a long GS1 string, a long URL) wrap rather than being clipped, in both light and dark
      themes, and on a small phone and a tablet.
- [x] 51. The launcher icon, the recents thumbnail and the splash screen show the IBE Group mark, not the .NET
      logo the template shipped. *(2026-09-28: the APK's `appicon_foreground.png` matches the mark at every
      density and the flattened icon is the mark on white; a launcher that caches icons may need a re-install
      to show it.)*
- [x] 52. The About tab opens with the IBE Group logo above the title. *(2026-09-28.)*

## iOS — requires a Mac, and a device for anything camera/NFC related

- [ ] 35. App builds and launches on the simulator; the camera page explains the missing camera instead of
      crashing. (The iOS simulator has no camera.)
- [ ] 36. On a device: checks 1–21 above, with particular attention to check 3 (UPC-A reported as EAN-13
      without normalization is the classic iOS symptom).
- [ ] 37. The camera permission prompt text is translated for each language (`InfoPlist.strings`).
- [ ] 38. The photo-library permission prompt is translated and photo scanning works.
- [ ] 39. The NFC tab explains that iOS NFC needs the Apple entitlement, and the message is translated —
      it must not look like a crash or a bug.
- [ ] 40. On an iPad, the layout is usable and the app stays in portrait (upside-down allowed) as on iPhone.

## Release

- [ ] 41. Release build of the Android app installs and scans (Release differs from Debug in linking and
      trimming; a decoder regression here would be invisible in Debug).
- [ ] 42. Release build size and start-up time are acceptable on a low-end device.
- [ ] 43. Confirm the app declares only camera, vibrate and NFC permissions in the built manifest.
