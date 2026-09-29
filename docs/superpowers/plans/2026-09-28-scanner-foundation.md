# Scanner Foundation Plan (Phases 1–2)

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for
> tracking.

**Goal:** Stand up the repository — solution layout, MIT license, the `net10.0` core library with its data
model, the xUnit test project, and the .NET MAUI 10 app shell with the four-language resource setup.

**Architecture:** Two projects, mirroring IBE Barcode Generator: `IBEBarcode.Scanner` (MAUI app,
`net10.0-android;net10.0-ios`) and `IBEBarcode.Scanner.Core` (`net10.0`, no MAUI dependency). Everything
that can be decided without a device lives in Core, where it is unit-testable; the app owns views, view
models and the three genuinely platform-specific services (camera engine, NFC reader, frame capture).

**Tech stack:** .NET 10, .NET MAUI 10, CommunityToolkit.Mvvm 8.4.2, BarcodeScanning.Native.Maui 3.1.0
(MIT), xUnit 2.9.3 with Microsoft.NET.Test.Sdk 17.14.1 / xunit.runner.visualstudio 3.1.4 /
coverlet.collector 6.0.4 — the same test stack the generator repository uses.

**Spec:** `docs/superpowers/specs/2026-09-28-ibe-barcode-scanner-design.md`

## Global Constraints

(Same as the generator's plans.) Every project targets .NET 10; MIT license, `Copyright (c) 2026 IBE
Group, Inc.`; no database; single solution file; `Nullable` and `ImplicitUsings` enabled everywhere;
English is the neutral language, with `zh-Hans`, `zh-Hant` and `ja` resource files.

---

### Task 1: Repository scaffolding — DONE

**Files:** `IBEBarcodeScanner.slnx`, `LICENSE`, `.gitignore`, `README.md`

- [x] Single `.slnx` with `/src/` (app, core) and `/tests/` folders, matching the generator's layout.
- [x] MIT `LICENSE` using the same copyright line as the generator repository.
- [x] `.gitignore` covering `bin/`, `obj/`, IDE files, Android/iOS build output and signing material.
- [x] `README.md` with status, solution layout, build/test commands, format-coverage table and known limitations.

### Task 2: Core project and data model — DONE

**Files:** `src/IBEBarcode.Scanner.Core/{IBEBarcode.Scanner.Core.csproj,BarcodeSymbology.cs,ScanKind.cs,ScanResult.cs,ScanResultFactory.cs,ScanDeduplicator.cs}`

- [x] `BarcodeSymbology` mirrors `IBEBarcode.Core.BarcodeSymbology` (plus `Unknown`) so scanned labels are
      named with the generator's vocabulary.
- [x] `ScanKind` and `ScanSource` describe what a payload is and where it came from.
- [x] `ScanResult` carries symbology, kind, raw text, display text, source, timestamp, link, GS1 elements,
      ISBN-13/10 and expanded UPC-A, and exposes a culture-neutral `SymbologyName`.
- [x] `ScanResultFactory` is the single place where a raw reading becomes a result: classify → normalize →
      build.
- [x] `ScanDeduplicator` suppresses repeats of the same value/symbology inside a time window, with a bounded
      cache so a long scan session cannot grow without limit.
- [x] `InternalsVisibleTo` for the test project.

### Task 3: Test project — DONE

**Files:** `tests/IBEBarcode.Scanner.Core.Tests/IBEBarcode.Scanner.Core.Tests.csproj`

- [x] Package versions copied from the generator's test project, `RootNamespace` set, `IsTestProject` set.
- [x] `ProjectReference` to Core.
- [x] `GeneratorPatterns` helper renders the generator's exact MSI Plessey and Postnet patterns (bar/space
      widths, bar heights, guard bars, check digit) so decoder tests need no device and no camera.

### Task 4: MAUI app shell — DONE

**Files:** `src/IBEBarcode.Scanner/{IBEBarcode.Scanner.csproj,MauiProgram.cs,App.xaml(.cs),AppShell.xaml(.cs)}`, `src/IBEBarcode.Scanner/Views/*`, `src/IBEBarcode.Scanner/ViewModels/*`

- [x] App project trimmed to iOS and Android (`net10.0-android;net10.0-ios`); the template's Mac Catalyst and
      Windows targets removed, along with their platform folders.
- [x] App identity: `net.ibegroup.ibebarcodescanner`, display name "IBE Barcode Scanner", version 1.0.0.
- [x] `MinimumOSVersion` support levels set to the library's minimums (Android 24, iOS 15.1).
- [x] `MauiProgram` registers services, view models and pages; `UseBarcodeScanning()` wires the camera control.
- [x] `AppShell` with four tabs, titles assigned from resources, pages resolved through DI-backed
      `DataTemplate` factories.
- [x] Four pages: `ScanPage` (camera + result card), `NfcPage`, `HistoryPage`, `AboutPage` (with the language
      picker), each with its view model and a shared `ScanResultCard` content view.

### Task 5: Localization plumbing — DONE

**Files:** `src/IBEBarcode.Scanner/Resources/Strings/AppResources.resx` (+ `.zh-Hans`, `.zh-Hant`, `.ja`),
`src/IBEBarcode.Scanner/Services/LanguageService.cs`, `src/IBEBarcode.Scanner/Platforms/{Android/Resources/values*/strings.xml,iOS/Resources/*.lproj/InfoPlist.strings,iOS/Info.plist}`

- [x] 90 string keys per language, with identical key sets in all four files.
- [x] `<NeutralLanguage>en</NeutralLanguage>` so unsupported cultures fall back to English rather than blanks.
- [x] Strongly-typed accessor generated during the build (`StronglyTypedLanguage` items), which is the
      documented .NET MAUI 10 setup when there is no Visual Studio designer step.
- [x] `LanguageService` applies the saved or device language before the first page exists, and rebuilds the
      shell on change so every string (including tab titles) refreshes.
- [x] iOS `CFBundleLocalizations` (en, zh-Hans, zh-Hant, ja) and `CFBundleDevelopmentRegion`, because iOS
      otherwise silently ignores the localized resources.
- [x] Android per-locale `app_name`, iOS per-locale `InfoPlist.strings` for the camera/photo/NFC prompts.
- [x] Tests assert identical key sets, no empty values, and that the Chinese/Japanese files are genuinely
      translated (with a short allow-list for values that are Latin script or endonyms by design).

## Verification

```bash
dotnet build IBEBarcodeScanner.slnx        # succeeds: Core, tests, app for Android and iOS
dotnet test                                # 128 tests, all passing
```

Deviations from the plan as first written, recorded deliberately:

- Localization lives in `Resources/Strings` (the .NET MAUI convention) rather than `Resources/Localization`.
- The app was scaffolded from the `dotnet new maui` template and then trimmed, rather than written by hand,
  so the styles, icon and splash assets follow the current template.
- No PNG fixtures from the generator app are committed (see the fallback-decoder plan): decoder tests
  synthesise the generator's patterns instead, which needs no manual export step.
