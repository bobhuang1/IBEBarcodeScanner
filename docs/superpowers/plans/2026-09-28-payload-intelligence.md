# Payload Intelligence Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Decide what a scanned value *is* — link, email, phone, SMS, WiFi, contact, location, calendar,
GS1 element string, retail product code or plain text — and normalize the retail formats so the value
shown matches the label that was printed. This is the layer that makes a scanner feel like it understands
barcodes instead of just echoing bytes.

**Architecture:** All of it lives in `IBEBarcode.Scanner.Core`, with no MAUI or platform dependency, so
every rule is unit-testable on any OS. `PayloadClassifier` is the entry point; `Gs1AiParser` and
`UpcEanNormalizer` are the two specialists it calls; `ScanResultFactory` composes the final `ScanResult`.

**Tech Stack:** .NET 10, xUnit. No third-party parsing libraries: the GS1 AI table and the UPC/EAN/ISBN
check-digit arithmetic are both small and worth owning outright.

**Spec:** `docs/superpowers/specs/2026-09-28-ibe-barcode-scanner-design.md`

## Global Constraints

(Same as prior plans.) `net10.0`; MIT; no database; `Nullable`/`ImplicitUsings` enabled; no culture-sensitive
formatting in classifications.

## Security-relevant decision

A scanned barcode is untrusted input: anyone can print a QR code that contains `javascript:`,
`file:///etc/passwd`, an `intent://` URL or a custom app scheme. The classifier is therefore the only
component allowed to decide that something is openable, and it only ever returns a `Uri` for `http` and
`https`. Everything else keeps its own `ScanKind` so the UI can describe it, and is never handed to the
platform launcher. This is asserted by tests (`Classify_HostileScheme_IsNeverOpenable`).

---

### Task 1: GS1 Application Identifier parsing — DONE

**Files:** `src/IBEBarcode.Scanner.Core/{Gs1AiParser.cs,Gs1Element.cs}`

- [x] AI table with fixed lengths and human-readable names for the AIs a scanner realistically meets
      (00, 01, 02, 10–22, 30, 37, 90–99, 235–255, 400–427, 4300–4308, 7001–7240, 710–716, 8001–8112, 8200).
- [x] Compact notation: split on the FNC1/group separator (ASCII 29, which ML Kit and Apple Vision both
      preserve in the raw payload), then consume fixed-length AIs without needing a separator after them.
- [x] Bracketed notation `(01)09501101530003(17)251231` — the form IBEBarcodeGenerator's GS1-128 encoder
      accepts and prints as human-readable text.
- [x] Unknown AIs keep their data as an element with no description rather than being dropped.
- [x] `LooksLikeGs1` for the "is this Code 128 actually GS1-128?" question, documented as a loose heuristic.

### Task 2: UPC/EAN/ISBN normalization — DONE

**Files:** `src/IBEBarcode.Scanner.Core/UpcEanNormalizer.cs`

- [x] Mod-10 check digit computed from the right with alternating 3/1 weights (EAN-13, EAN-8, UPC-A).
- [x] EAN-13 with a leading zero and a valid UPC-A body → reported as UPC-A, with the leading zero removed.
      iOS reports UPC-A this way, so without this the app would display a different code than the label.
- [x] Bookland 978/979 → ISBN, with ISBN-10 derived for 978 ranges (including the `X` check-digit case).
- [x] UPC-E expansion to UPC-A for 6-digit (implicit number system) and 8-digit (with number system and check
      digit) forms, verified against published reference pairs.
- [x] Normalization is idempotent: normalizing an already-normalized value changes nothing.

### Task 3: Payload classification — DONE

**Files:** `src/IBEBarcode.Scanner.Core/{PayloadClassifier.cs,ScanResultFactory.cs}`

- [x] Recognises vCard/MECARD, `BEGIN:VEVENT`, `WIFI:`/`WPA:`, GS1 content, URI schemes, scheme-less
      domains and retail codes, in that order.
- [x] Scheme-less domains such as `www.example.com/pricing` become `https://…` links, but only when the host
      has a plausible TLD — so `123.45` and sentences stay text.
- [x] `tel:`, `mailto:`, `sms:`/`smsto:`, `geo:` keep their own kinds and are never opened by the app.
- [x] GS1 element strings are rendered in bracketed notation for display, so copy/paste does not carry
      invisible separator characters.
- [x] The classifier carries the normalized symbology forward, so a UPC-A read as EAN-13 is reported as
      UPC-A all the way through to the result card.

### Task 4: Displaying the result — DONE

**Files:** `src/IBEBarcode.Scanner/ViewModels/ScanResultViewModel.cs`, `src/IBEBarcode.Scanner/Views/ScanResultCard.xaml`

- [x] One view model and one card for every path (camera, NFC, photo, history), so a result looks the same
      wherever it came from.
- [x] Symbology, kind, value, raw value (only when it differs), GS1 element list, ISBN-13/10, expanded
      UPC-A, source and timestamp, with localized labels.
- [x] Actions limited to what makes sense: open-in-browser only when the classifier produced a link, plus
      copy and share.
- [x] An unverified check digit produces a visible warning rather than a silent wrong answer.

## Verification

```bash
dotnet test tests/IBEBarcode.Scanner.Core.Tests --filter "FullyQualifiedName~PayloadClassifier|FullyQualifiedName~Gs1AiParser|FullyQualifiedName~UpcEanNormalizer|FullyQualifiedName~ScanResult"
```

Covers: hostile/unknown schemes, bare domains, decimal numbers and sentences that must *not* become links,
fixed and variable GS1 AIs with and without separators, unknown AIs, ISBN-10 derivation, UPC-E expansion
reference pairs, check-digit arithmetic, and empty/null inputs on every entry point.
