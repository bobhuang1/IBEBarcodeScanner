# Packaging, signing and CI/CD

Everything about turning this repository into files somebody can install: what GitHub Actions
produces, what a keystore is for, what an iPhone build actually needs, and what has and has not
been verified. The two workflows are [build.yml](../../.github/workflows/build.yml) (every push)
and [release.yml](../../.github/workflows/release.yml) (tags and manual runs).

Neither workflow has ever run: this repository had no remote when they were written. Everything
below that is marked *verified* was reproduced by hand on the development machine
(`dotnet publish` invocations and the artifacts they produced); everything else is a statement
about what the workflow is supposed to do.

## The Android flavors

`-p:AndroidAbi` (defined in [IBEBarcode.Scanner.csproj](../../src/IBEBarcode.Scanner/IBEBarcode.Scanner.csproj))
selects a single ABI. It has to be a property of our own rather than `-p:RuntimeIdentifier`
because MSBuild global properties are passed to every referenced project as well, and
`IBEBarcode.Scanner.Core` targets plain `net10.0`, where `android-arm` resolves through the RID
graph to `linux-bionic-arm` and restore dies with:

```
error NU1101: Unable to find package Microsoft.NETCore.App.Runtime.linux-bionic-arm
```

| Flavor | Command | ABI content | Release size |
|---|---|---|---|
| `arm64-v8a` | `-p:AndroidAbi=android-arm64` | `lib/arm64-v8a` | 26.1 MB *(verified)* |
| `armeabi-v7a` | `-p:AndroidAbi=android-arm` | `lib/armeabi-v7a` | 25.9 MB *(verified)* |
| `x86_64` | `-p:AndroidAbi=android-x64` | `lib/x86_64` | not measured |
| `universal` | (no `AndroidAbi`) | all three | 52.8 MB *(verified)* |
| `bundle` | `-p:AndroidPackageFormat=aab` | all three, Play splits it | 52.1 MB *(verified)* |

Which one to hand out: `universal` if the phone is unknown, `arm64-v8a` for anything modern,
`armeabi-v7a` only for old or very cheap handsets (the `C7V240416051117` test phone is one of
these, so that flavor has to keep being built), `x86_64` for emulators and a few Chromebooks, and
the `.aab` only for Google Play.

Two mechanical details worth knowing before changing the workflow:

- A bundle build does not produce *only* a bundle: `dist/` ends up with `-Signed.aab`, an
  unsigned `.aab` and an APK (verified). Anything that picks a file by extension alone will
  grab the wrong one, which is why the workflow matches `*-Signed.aab`.
- `-p:RuntimeIdentifiers="android-arm64;android-arm"` does not work as a command-line override.
  The semicolon is a property separator to MSBuild, so the value reaches the Core project as a
  literal RID and the restore fails.

## Signing an Android release

Android will not install an unsigned APK, and an APK signed with a different key cannot update
one already installed — the key *is* the app's identity. Debug builds are signed with a keystore
`.NET for Android` generates per machine (`%LOCALAPPDATA%\Xamarin\Mono for Android\debug.keystore`,
`CN=Android Debug`), which is fine for a phone on a desk and useless for anything else, including
successive CI runs: each runner generates its own, so a debug-signed build from yesterday cannot
be upgraded by one from today.

The keystore does not exist yet. Create it once, with the JDK that is already installed, and store
it outside the repository (`*.keystore` and `*.jks` are gitignored — not a substitute for keeping
the only copy somewhere safe):

```bash
"/c/Program Files/Android/openjdk/jdk-21.0.8/bin/keytool" -genkeypair \
  -keystore ibe-barcodescanner.keystore -alias ibe-barcodescanner \
  -keyalg RSA -keysize 2048 -validity 10000
```

The same keystore signs every future release. If it is lost, existing installs can never be
updated again, only uninstalled and replaced. Answer the prompts with the IBE Group details; they
end up in the certificate, not on screen.

Repository secrets (Settings → Secrets and variables → Actions):

| Secret | Value |
|---|---|
| `ANDROID_KEYSTORE_BASE64` | `base64 -w0 ibe-barcodescanner.keystore` |
| `ANDROID_KEYSTORE_PASSWORD` | the keystore password |
| `ANDROID_KEY_ALIAS` | `ibe-barcodescanner` |
| `ANDROID_KEY_PASSWORD` | the key password (same as the store password unless told otherwise) |

With those set, both workflows sign with the release key, so push builds and releases update each
other. Without them the Android jobs still produce working APKs signed with the runner's debug
key and print a GitHub warning annotation; the release notes then say what signed what. Passwords
are written to files and handed to MSBuild as `file:` — never `env:` and never on the command
line — so they do not appear in a build log. (`env:` is documented as unsupported for `.aab`
builds in any case.)

If this ever goes to Google Play, the first upload switches on Play App Signing: Google then holds
the app signing key and this keystore becomes the *upload* key. Nothing about the commands
changes.

Manual equivalent of what CI does:

```bash
export JAVA_HOME="/c/Program Files/Android/openjdk/jdk-21.0.8"
dotnet publish src/IBEBarcode.Scanner/IBEBarcode.Scanner.csproj -f net10.0-android -c Release \
  -p:AndroidAbi=android-arm64 -p:AndroidPackageFormat=apk -o dist \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore=ibe-barcodescanner.keystore \
  -p:AndroidSigningKeyAlias=ibe-barcodescanner \
  -p:AndroidSigningKeyPass=file:storepass.txt \
  -p:AndroidSigningStorePass=file:storepass.txt

"/c/Program Files (x86)/Android/android-sdk/build-tools/36.0.0/apksigner.bat" \
  verify --print-certs dist/net.ibegroup.ibebarcodescanner-Signed.apk
```

`apksigner` needs `JAVA_HOME` set; without it it fails with `ERROR: JAVA_HOME is not set`.

## The iPhone side

An `.ipa` can only be produced on macOS, and only signed by an Apple-issued certificate. There is
no way around either from this repository, so the honest summary is:

| What you want | What it needs | Cost |
|---|---|---|
| Simulator build in CI (compile signal) | Xcode, nothing else | free, already in `build.yml` |
| IPA installing on *your* iPhone (7 days, then re-install) | a free Apple ID: development certificate + development profile with the device UDID registered | free |
| Ad hoc IPA for a fixed list of devices (1 year, 100 devices) | paid Apple Developer Program | $99/year |
| TestFlight / App Store | paid membership + App Store Connect record | $99/year |
| Anything at all with **iOS NFC** | paid membership: the CoreNFC entitlement is program-only | $99/year |

The free-account path is genuinely useful for testing and is what the workflow supports: generate
the certificate and profile in Xcode on any Mac (or in the developer portal), register
`net.ibegroup.ibebarcodescanner` as an App ID, and export the certificate as a `.p12`. Then set:

| Secret | Value |
|---|---|
| `IOS_CERTIFICATE_P12_BASE64` | `base64 -i certificate.p12 \| pbcopy` (macOS) or `base64 -w0` on Linux |
| `IOS_CERTIFICATE_PASSWORD` | the export password of the `.p12` |
| `IOS_PROVISIONING_PROFILE_BASE64` | the downloaded `.mobileprovision`, base64-encoded |
| `IOS_CODESIGN_KEY` | the certificate's common name, e.g. `Apple Development: IBE Group (ABCDE12345)` |
| `IOS_PROVISIONING_PROFILE_NAME` | the profile's name as shown in the portal |

With `IOS_CERTIFICATE_P12_BASE64` present the job imports the certificate into a temporary
keychain, drops the profile where `codesign` looks for it, and runs the documented publish:

```bash
dotnet publish src/IBEBarcode.Scanner/IBEBarcode.Scanner.csproj -f net10.0-ios -c Release \
  -p:ArchiveOnBuild=true -p:RuntimeIdentifier=ios-arm64 \
  -p:CodesignKey="Apple Development: IBE Group (ABCDE12345)" \
  -p:CodesignProvision="IBE Barcode Scanner Development"
```

The IPA lands in `bin/Release/net10.0-ios/ios-arm64/publish/`. Without those secrets the job
*skips* the publish and explains itself in the run summary rather than failing — a missing Apple
account is a configuration state, not a broken build.

Two options exist for the cases where the defaults do not fit, both set as repository *variables*
(Settings → Secrets and variables → Actions → Variables), both off by default:

- `IOS_DOWNLOAD_SIMULATOR_RUNTIME=1` runs `xcodebuild -downloadPlatform iOS` before the build. Some
  runner images ship an iOS SDK whose simulator runtime is missing, which makes `actool` fail with
  `No simulator runtime version ... available to use with iphonesimulator SDK version ...` even for
  a device build (dotnet/macios#25298, #25473). The download is several GB, so it is opt-in.
- `IOS_TRY_UNSIGNED_IPA=1` attempts an unsigned `.ipa` (`-p:CodesignKey=`) and, failing that, zips
  the `.app` into a `Payload/` folder. **This is experimental and unverified.** An unsigned IPA is
  not installable as it stands; it is only useful to somebody who re-signs it themselves
  (Sideloadly, AltStore, a jailbroken device).

## What the workflows do

`build.yml` — pushes to `master`/`main`, pull requests, manual runs:

| Job | Runner | Result |
|---|---|---|
| tests | ubuntu | `dotnet test` on the Core suite |
| android | ubuntu | Release APK (all ABIs), release-signed when the keystore secrets exist, uploaded as `android-apk` (14 days) |
| ios | macos-26 | simulator build, Xcode pinned, no signing material, nothing uploaded |

`release.yml` — tags matching `v*`, or a manual run:

| Job | Runner | Result |
|---|---|---|
| tests, version | ubuntu | test gate; version from the project file (or the tag) and run number as the version code |
| android × 5 | ubuntu | one artifact per flavor, each signature recorded in a `signing-info-*.txt` |
| ios-ipa | macos-26 | signed `.ipa` when configured, otherwise a step summary explaining what is missing |
| release | ubuntu | downloads every artifact and attaches them to the GitHub Release with generated notes |

The release only happens for a tag (`git tag v1.0.1 && git push origin v1.0.1`) or when a manual
run is given a tag. A manual run without one is a build: the artifacts stay in the run and nothing
is published. Bump `<ApplicationDisplayVersion>` in the project file before tagging;
`<ApplicationVersion>` comes from the run number, because Android requires a version code that
increases with every upload.

The release job needs `contents: write` for `GITHUB_TOKEN` — it creates the release with the
`gh` CLI (which is on every runner) rather than a third-party action.

### Cost

Public repositories have no minute quota. On a private repository macOS jobs consume the included
minutes at roughly 10× the Linux rate, so a release (5 short Ubuntu jobs, one ~20-minute macOS job)
costs on the order of 200 of the 2000 minutes a free private plan includes — a handful of releases
a month is not a problem, a release per push would be.

## First-run expectations

Things that are most likely to need attention the first time these run, in the order they would
appear:

1. **The Android workload on Linux.** `dotnet workload install maui-android` pulls the Android
   SDK's own dependencies; the image already carries the SDK and a JDK.
2. **`NETSDK1147: workload must be installed`.** Restoring the multi-targeted app project resolves
   both target frameworks, so the iOS job needs the Android workload too — which is why it installs
   `maui` rather than `maui-ios`.
3. **Xcode versus the workload band.** The .NET for iOS workload here is 26.5, and Xcode 26.6 is
   what the `macos-26` image ships. Both workflows pin Xcode explicitly so an image update cannot
   silently move it; a future .NET for iOS band will need the list in the `Select Xcode` step
   updated to match.
4. **`java` for `apksigner`** — present on the Ubuntu images, absent from this Windows machine
   unless `JAVA_HOME` is set by hand.

## Verification status

Verified by hand on 2026-09-29, on Windows, with SDK 10.0.401 and workload 10.0.401:

- `-p:AndroidAbi=android-arm` → 25.9 MB APK containing only `lib/armeabi-v7a`
- `-p:AndroidAbi=android-arm64` → 26.1 MB APK containing only `lib/arm64-v8a`
- `-p:AndroidPackageFormat=aab` → signed `.aab` (52.1 MB) plus an unsigned `.aab` and an APK
- `-p:RuntimeIdentifier=iossimulator-arm64 -f net10.0-ios` → clean build (no RID leak)
- the release workflow's artifact-naming and `dotnet publish` argument assembly, executed against
  a stub with the real publish directories reproduced for all five flavors

Not verified at all: any part of the iOS signed path, the keychain import, and the workflows
themselves — none of it has run, because the repository has no remote yet.
