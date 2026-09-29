# Release and CI Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use superpowers:subagent-driven-development or
> superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Make the repository build itself on every push, and get the app to the point where store submission
is a signing-and-metadata exercise rather than an engineering one.

**Architecture:** GitHub Actions. `build.yml` has three jobs — tests, Android build, iOS build — deliberately
split so the two platform-specific compilations fail independently. `release.yml` turns a `v*` tag into
installable files: three per-ABI APKs, a universal APK and a Play bundle, plus an iOS IPA, attached to a GitHub
Release. There is no server, no database and no deployment step: the artifacts are produced from a signing
configuration that stays in repository secrets, and every signing property is optional so a fork or a fresh
clone can still build.

**Spec:** `docs/superpowers/specs/2026-09-28-ibe-barcode-scanner-design.md`

## Global Constraints

(Same as prior plans.) .NET 10; MIT; no database; `Nullable`/`ImplicitUsings` enabled.

---

### Task 1: CI workflow — DONE

**Files:** `.github/workflows/build.yml`

- [x] `tests` job: restore, build the solution and run the Core test suite on `ubuntu-latest` (the logic under
      test has no platform dependency, so this job is fast and never flaky).
- [x] `android` job: install the `maui-android` workload, build `net10.0-android` in Release. Android builds on
      Linux, so this is a strong signal on every push.
- [x] `ios` job on `macos-latest`: install the `maui` workload, build `net10.0-ios` for the simulator (no
      signing material needed for compilation), with a comment pointing at the signing secrets for real
      device/archive builds.
- [x] `DOTNET_SKIP_FIRST_TIME_EXPERIENCE` and NuGet caching kept simple — premature optimization here is a
      liability, and the workloads dominate the first run.

### Task 2: App metadata and privacy — DONE

**Files:** `src/IBEBarcode.Scanner/IBEBarcode.Scanner.csproj`, `Platforms/iOS/Resources/PrivacyInfo.xcprivacy`, `Platforms/Android/AndroidManifest.xml`

- [x] Display name, identifier (`net.ibegroup.ibebarcodescanner`), display version and build version set.
- [x] App icon and splash use the same brand colour as the generator's palette, from the MAUI template assets.
- [x] iOS privacy manifest present (the template's), covering the fact that the app uses no tracking APIs,
      collects no data and stores nothing outside the device.
- [x] The Android manifest declares exactly three permissions — camera, vibrate, NFC — with camera and NFC
      features optional so the app installs broadly.

### Task 3: Documentation — DONE

**Files:** `README.md`, `docs/superpowers/specs/*`, `docs/superpowers/plans/*`, `docs/testing/device-checklist.md`

- [x] README in the generator repository's style: status, layout, build/test commands, coverage table, NFC
      status, known limitations, license.
- [x] Dated design spec and one plan per subsystem, each stating its goal, architecture, tasks and verification.
- [x] A device checklist that separates what has been verified by tests (everything in Core) from what has not
      (anything requiring a camera or a tag).

### Task 4: Store readiness — PARTIALLY DONE, NOT BLOCKED ON CODE

- [x] Free/MIT licensing recorded, dependencies audited as MIT or royalty-free platform frameworks
      (`BarcodeScanning.Native.Maui` MIT; ML Kit and Apple Vision are platform frameworks).
- [x] Play data-safety answers are answerable from the code: camera and NFC inputs, no data collected, no data
      shared, no analytics SDK, nothing persisted beyond local settings.
- [ ] Apple signing with the CoreNFC entitlement (a prerequisite for iOS NFC, not for release).
- [ ] Signing secrets for Play/App Store uploads, and a real `Release` archive build.
- [ ] Decide whether to publish to Chinese app stores (separate onboarding, and a China-specific note about
      Google Play services being unavailable, which affects the Android camera path on some devices there).

### Task 5: Packaging and CI/CD — WORKFLOWS DONE, CREDENTIALS OUTSTANDING

**Files:** `.github/workflows/build.yml`, `.github/workflows/release.yml`,
`src/IBEBarcode.Scanner/IBEBarcode.Scanner.csproj`, `docs/testing/packaging-and-ci.md`

- [x] `-p:AndroidAbi` in the project file, so one ABI can be packaged per build without leaking a
      `RuntimeIdentifier` global property into `IBEBarcode.Scanner.Core` (which fails restore with NU1101).
      Verified for `android-arm` and `android-arm64`: 25.9 MB and 26.1 MB APKs, each carrying one ABI.
- [x] `release.yml`: five Android flavors (three ABIs, universal, bundle) plus the iOS IPA, each artifact
      named for what it is, signed from repository secrets when they exist and debug-signed with a warning
      when they do not, and attached to a GitHub Release on a `v*` tag.
- [x] `build.yml`: pushes still get tests, an Android build and a simulator iOS build, and the Android job now
      uploads an installable APK so a build can reach a phone without a local toolchain. Xcode pinned in both
      workflows so a runner image update cannot silently move it.
- [ ] Create the release keystore and set `ANDROID_KEYSTORE_BASE64`, `ANDROID_KEYSTORE_PASSWORD`,
      `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASSWORD`. Until then every APK is debug-signed and cannot update the
      last one.
- [ ] Set the five `IOS_*` secrets (a free Apple ID is enough for a 7-day development build) and confirm a
      signed `.ipa` comes out.
- [ ] Push the repository to GitHub and watch the first run of both workflows. Nothing in either has executed
      yet, so the workload identifiers, the `macos-26` image contents and the Xcode version are all still
      assumptions.

## Verification

```bash
dotnet test                                 # the CI test job, locally
dotnet build src/IBEBarcode.Scanner -f net10.0-android -c Release
dotnet build src/IBEBarcode.Scanner -f net10.0-ios -c Release -p:RuntimeIdentifier=iossimulator-arm64
```

The workflows are committed but have not run: this repository has no remote yet. The first push is the moment
to confirm the workload identifiers and macOS runner image still match what the jobs assume. The packaging
matrix, the signing rules and the iOS constraints are written up in `docs/testing/packaging-and-ci.md`.
