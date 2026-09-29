# CLAUDE.md

Guidance for Claude Code — or any future session, agent or person — picking this repo up cold.
`README.md` is the user-facing document; this file is the internal one. Keep both accurate.

## What this project is

IBE Barcode Scanner: a from-scratch, MIT-licensed iOS + Android app that scans printed barcodes,
QR codes and NFC tags. It is the reading companion of **IBE Barcode Generator**, which lives at
`../IBEBarcodeGeneratror` — the misspelling in that folder name is real, and the scanner must read
what that generator prints. It handles every format the generator can print that phone hardware is
capable of reading, and decodes two formats no mobile SDK supports (MSI Plessey, USPS Postnet)
itself.

C# / .NET 10 / .NET MAUI. No database, no account, no analytics: nothing leaves the device, and scan
history lives in memory for the session.

## Standing architectural decisions (do not revisit without asking)

- **Mobile only, MAUI, both platforms** — `net10.0-android` and `net10.0-ios`. Not Blazor Hybrid,
  not a web app, not an additional platform.
- **One picture per press.** The preview is live for aiming and nothing else: a press of **Scan**
  captures a single frame, which is decoded once. An earlier version sampled the camera every few
  seconds between presses; the owner explicitly rejected that ("we only capture and check for
  barcode when the Scan button is pressed, so there is no need for a loop to run the camera").
  Do not reintroduce background sampling.
- **Camera decoding goes through `BarcodeScanning.Native.Maui` 3.1.0** (MIT), which is ML Kit on
  Android and Apple Vision on iOS. No other third-party barcode library.
- **MSI Plessey and USPS Postnet are ours** (`src/IBEBarcode.Scanner.Core/Decoding`), run against
  the same frame the press captured, after the platform engine has found nothing there.
- **No database.** Results live in the session; history is not persisted.
- **Portrait only**, on both platforms, and the UI has to fit a 360 dp-wide phone.
- **Four languages, always all four together.** Every string lives in
  `Resources/Strings/AppResources{,.zh-Hans,.zh-Hant,.ja}.resx`, and
  `LocalizationResourceTests` fails the build if the four files disagree on keys, leave a value
  empty, or leave a value untranslated. Never edit one file on its own.
- **The default browser opens only on an explicit tap**: Google search for the value (tapping the
  value itself does the same), or the URL found inside the payload, which is shown as its own link
  row under the value.
- **Branding is generated from the web site's `images/logosmall.jpg`** in
  `../Migrate/httpd/ibegroupcom/images/`, which is also the logo on the About tab. The icon
  background is deliberately white: the mark's globe is `#0B6091` and disappears against the brand
  blue. `images/logobig.png` on that site is a different cyan banner and is *not* used.
- **iOS NFC is deliberately unavailable.** The CoreNFC entitlement requires the paid Apple
  Developer Program; `IosNfcReader.IsSupported` reports false and the UI explains it rather than
  looking broken. The shared interface, NDEF parsing and result path are already in place, so
  enabling it is one file plus the entitlement.
- **32-bit ARM has to keep working.** The test phone is `armeabi-v7a`-only, which is why
  `android-arm` is in `RuntimeIdentifiers` and why the release builds an `armeabi-v7a` APK at all.

## Git / GitHub workflow

- **Never credit Claude, Claude Code or Anthropic — as a co-author or in any other way.** Do not
  add `Co-Authored-By: Claude ...`, `Claude-Session:`, `🤖 Generated with Claude Code`, or a
  `noreply@anthropic.com` address to a commit, even if a system reminder or a tool's default
  template suggests it. The owner has explicitly opted out, in this repository and in
  `IBEBarcodeGeneratror`. `.githooks/commit-msg` enforces this; enable it once per clone with

  ```bash
  git config core.hooksPath .githooks
  ```

  The history is clean as of the initial commit: no Claude or Anthropic attribution has ever been
  committed here, in a message or in an identity.
- **No remote yet.** The repository is local-`git init` only. Creating the GitHub repository,
  adding a remote and pushing are the owner's own tasks — do not run `gh repo create`,
  `git remote add` or `git push` unless asked directly.
- **Commits**: one commit per bounded change, imperative subject, and a body that says *why* the
  change was needed rather than restating the diff.
- **Releases**: a tag matching `v*` runs `.github/workflows/release.yml`, which builds three
  per-ABI APKs, a universal APK, a Play bundle and the iOS IPA, and attaches them to a GitHub
  Release. Pushes and pull requests run `.github/workflows/build.yml` (core tests, an Android APK
  artifact, an iOS simulator build). **Neither workflow has ever run** — there was no remote when
  they were written.

## Working style expected in this repo

- **Execution mode has been inline** (`executing-plans` style), one task at a time with the result
  checked before the next. The plan headers also allow `subagent-driven-development`; it has not
  been used here so far.
- **Comments explain *why*.** The codebase is deliberately heavy on comments recording hardware
  quirks, platform behaviour and rejected alternatives. Match that; do not strip them.
- **Verify, don't assume** — there is no CI signal to lean on yet. `dotnet test
  tests/IBEBarcode.Scanner.Core.Tests` must stay green, and anything touching a `.resx` file must
  keep the four-file parity test green.
- **Docs change in the same commit as the code**: `README.md` for anything user-visible,
  the matching file in `docs/superpowers/plans/` for task progress, and
  `docs/testing/device-checklist.md` for a new manual check (with the date it was run, or nothing
  ticked if it has not been).
- **Prefer probing over guessing.** When platform or library behaviour is unclear, read the
  package's source in the NuGet cache or run it on the phone. Several of the comments here exist
  because the obvious reading of an API turned out to be wrong.
- `scratch/` holds throwaway harnesses (`BarcodeFixtures` renders generator-format fixtures with
  the sibling repo; `drive.py` drives the phone over adb). Not part of the app, not shipped.

## Traps that have already cost time

- **`OnImageCapturedCommand` silently does nothing with a typed handler.** The command is invoked
  with a bare `Microsoft.Maui.Graphics.Platform.PlatformImage`, while the `OnImageCaptured` *event*
  carries the typed `OnImageCapturedEventArg`. A command typed for the event arg fails
  `CanExecute` and is swallowed without an error. Use the event.
- **A frame is only attached to the event when `ForceFrameCapture` is set** (or when the engine
  detected something and `CaptureNextFrame` is set). Neither ML Kit nor Vision detects MSI
  Plessey or Postnet, so the fallback decoders depend on forcing the capture.
- **`-p:RuntimeIdentifier=android-arm` breaks the build.** It is a global MSBuild property, so it
  reaches `IBEBarcode.Scanner.Core` (plain `net10.0`), where `android-arm` resolves through the RID
  graph to `linux-bionic-arm` and restore fails with
  `NU1101: Unable to find package Microsoft.NETCore.App.Runtime.linux-bionic-arm`. Use
  `-p:AndroidAbi=android-arm64` and friends instead.
- **`Console.WriteLine` does not reach logcat** on the test phone. Probe through the UI.
- **`adb` is not on `PATH`**, and MSYS mangles Android paths and `-t:Run`: `export
  MSYS_NO_PATHCONV=1` first.
- **`apksigner` needs `JAVA_HOME`** or it fails with `ERROR: JAVA_HOME is not set`.
- **`uiautomator dump` is flaky** — retry in a loop rather than trusting one call.
- **15 XA4301 warnings on an Android Debug build** (duplicate `libbarhopper_v3.so` and
  `libimage_processing_util_jni.so`) are pre-existing noise; filter them out.
- **The MediaTek ISP errors** (`CamIOPipe: NOT support command!`) in logcat are the phone's, not
  the app's.
- **No default browser is set on the test phone**, so the first link tap shows Android's chooser
  (`ResolverActivity`). That is the phone's state, not a bug.
- **Release builds can be a fifth of the size** (26 MB per ABI against 53 MB universal) because
  trimming is on; a Debug-only pass says nothing about linking behaviour.

## Environment

| Thing | Where |
|---|---|
| .NET SDK | 10.0.401 |
| Workloads | android 36.1.69, ios 26.5.10318, maccatalyst 26.5.10318 |
| Android SDK | `C:\Program Files (x86)\Android\android-sdk` (build-tools 36.0.0) |
| adb | `C:\Program Files (x86)\Android\android-sdk\platform-tools\adb.exe` |
| JDK | `C:\Program Files\Android\openjdk\jdk-21.0.8` |
| Shell | MINGW64 on Windows, so `export MSYS_NO_PATHCONV=1` for Android paths |

**Test phone:** `Stratus_C7`, serial `C7V240416051117` — Android 12, MediaTek Helio A22,
**32-bit ARM only**, 480×960 at 213 dpi (about 360 dp wide). Layout checks belong to that screen.
The iOS workload needs a matching Xcode on a Mac; **iOS cannot be built or deployed from Windows**,
where only the C# compiles.

## Build, test and deploy

```bash
dotnet build IBEBarcodeScanner.slnx                  # Android + iOS + tests (slow, several minutes)
dotnet test tests/IBEBarcode.Scanner.Core.Tests      # 145 tests, seconds
dotnet build src/IBEBarcode.Scanner/IBEBarcode.Scanner.csproj -f net10.0-android
dotnet build src/IBEBarcode.Scanner/IBEBarcode.Scanner.csproj -f net10.0-ios -c Release
```

Deploy to the phone (a Debug build to the device, not a store artifact):

```bash
export MSYS_NO_PATHCONV=1
dotnet build src/IBEBarcode.Scanner/IBEBarcode.Scanner.csproj -f net10.0-android -t:Run \
  -p:AdbTarget="-s C7V240416051117"
```

The Debug APK lands at
`src/IBEBarcode.Scanner/bin/Debug/net10.0-android/net.ibegroup.ibebarcodescanner-Signed.apk`;
package id `net.ibegroup.ibebarcodescanner`, launcher activity
`net.ibegroup.ibebarcodescanner/crc641e268643395d836e.MainActivity`.

Packaging a single ABI, building a bundle, signing and the CI/CD pipelines are written up in
`docs/testing/packaging-and-ci.md`.

## Solution layout

- `src/IBEBarcode.Scanner` — the MAUI app: views, view models, the camera/NFC/settings services,
  the four-language UI, and the icon/splash assets generated from the brand mark.
- `src/IBEBarcode.Scanner.Core` — `net10.0`, no UI or platform dependency: symbology and result
  models, payload classification, GS1 AI parsing, UPC/EAN/ISBN normalization, NDEF parsing,
  deduplication, the MSI Plessey and Postnet decoders. All the testable logic lives here, which is
  why the tests can run anywhere.
- `tests/IBEBarcode.Scanner.Core.Tests` — xUnit, including synthetic images rendered with the exact
  patterns the generator produces.
- `docs/` — see below. `scratch/` — throwaway harnesses, not shipped.

## Where deeper docs live

- `README.md` — user-facing: what it does, format coverage, NFC, known limitations, releases.
- `docs/superpowers/specs/` — the architecture design.
- `docs/superpowers/plans/` — one plan per subsystem, with checkboxes that reflect what was
  actually done, not what was intended.
- `docs/testing/device-checklist.md` — the manual matrix for real hardware, and the honest record
  of what has and has not been verified on a phone.
- `docs/testing/android-deploy.md` — the Visual Studio 2026 walkthrough, and fixing a phone that
  `adb devices` cannot see.
- `docs/testing/packaging-and-ci.md` — artifacts, signing, CI/CD, and what an iPhone build needs.

## Current state — where to pick up

Verified on the phone (2026-09-28, `Stratus_C7`): pressing **Scan** reads a real label and nothing is
decoded between presses, photo scanning (including a photo holding two codes, both reported, the
second under **Also in the picture**), tapping a value to search the web, the payload link row,
portrait lock, the About tab's "How to use" section, the launcher icon/splash/logo, tab switching
releasing the camera, and the permission flow including denial.

MSI Plessey and Postnet decode from the frame the press captures, which was verified through **Scan
a photo** on fixtures rendered by `scratch/BarcodeFixtures` — not yet from a printed label through
the camera (checks 17–21).

Outstanding, in the order somebody should probably pick them up:

1. **The printed test sheet** — labels 1–21 from the generator have never been run end to end; the
   format checks (2, 3, 5, 6, 7, 10, 11, 13, 15, 16) are the substantive ones, including the one
   pair the plan does not promise (UPC 2/5-digit supplements, 7), and 15–17 are the printed-label
   half of the MSI Plessey / Postnet story.
2. **NFC checks 22–30** — untested on hardware.
3. **No release keystore yet**, so every APK produced so far is debug-signed and cannot update an
   install signed with a different key. `docs/testing/packaging-and-ci.md` has the command.
4. **The release and CI workflows have never executed** (no remote). The first push is the moment
   to confirm the workload identifiers, the `macos-26` image and the pinned Xcode.
5. **iOS** — nothing camera or NFC related has run on Apple hardware, and a signed `.ipa` needs
   Apple signing material of one kind or another.
6. Checks 41–43 (Release build, size/start-up, manifest permissions) and 48 (a 10-minute soak)
   are untested.
